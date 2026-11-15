using Polhem.Db.Providers;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.Schema
{
    /// <summary>
    /// Dialect-neutral schema shaping shared by every provider's table-rebuild command builder:
    /// deriving the effective rebuild schema from a diff, cloning it under the temporary table
    /// name, and stripping secondary indexes.
    /// </summary>
    /// <remarks>
    /// The rebuild <em>script</em> is dialect-specific (drop / create / copy / rename all differ),
    /// but the schema objects fed into it are not. Extracted from five identical per-provider
    /// copies so a change to the extension-field policy applies to all providers at once.
    /// </remarks>
    internal static class RebuildSchemaFactory
    {
        /// <summary>
        /// Builds the effective rebuild schema: the defined table plus any real-only fields appended
        /// (extension field policy — columns present in the database but absent from the definition
        /// are preserved through the rebuild).
        /// </summary>
        /// <remarks>
        /// A column the definition renames is not an extension field: it is copied into its new name (see
        /// <see cref="GetSourceFieldNames"/>). An extension field keeps the default read back from the database
        /// when every dialect can emit it; any other default, an expression such as a built-in default an earlier
        /// framework version used, becomes the framework's built-in default for the column's type.
        /// </remarks>
        /// <param name="diff">The schema diff being rebuilt.</param>
        public static TableSchema BuildEffectiveSchema(TableSchemaDiff diff)
        {
            var cloned = diff.DefineTable.Clone();
            foreach (var realField in GetExtensionFields(diff))
            {
                var extension = realField.Clone();
                if (!DefaultValueLiteral.CanEmit(extension))
                    extension.DefaultValue = string.Empty;
                cloned.Fields!.Add(extension);
            }
            return cloned;
        }

        /// <summary>
        /// Describes each extension field whose default the rebuild replaces with the built-in default, for the
        /// upgrade plan's warnings.
        /// </summary>
        /// <param name="diff">The schema diff being rebuilt.</param>
        public static IEnumerable<string> DescribeReplacedDefaults(TableSchemaDiff diff)
            => GetExtensionFields(diff)
                .Where(f => !DefaultValueLiteral.CanEmit(f))
                .Select(f => $"Column '{f.FieldName}' is not in the definition; the rebuild replaces its default " +
                             $"'{f.DefaultValue}' with the built-in default of {f.DbType}.");

        /// <summary>
        /// Returns the columns the database has and the definition does not declare, leaving out the columns the
        /// definition renames.
        /// </summary>
        private static IEnumerable<DbField> GetExtensionFields(TableSchemaDiff diff)
        {
            if (diff.RealTable == null)
                return [];
            var sourceNames = GetSourceFieldNames(diff).Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return diff.RealTable.Fields!.Where(f =>
                !diff.DefineTable.Fields!.Contains(f.FieldName) && !sourceNames.Contains(f.FieldName));
        }

        /// <summary>
        /// Returns the source column of each field the diff renames, keyed by the field's new name.
        /// </summary>
        /// <param name="diff">The schema diff being rebuilt.</param>
        /// <remarks>
        /// The source name is spelled as the database reports it, which matters where column names are compared by
        /// case (PostgreSQL).
        /// </remarks>
        public static Dictionary<string, string> GetSourceFieldNames(TableSchemaDiff diff)
            => diff.Changes.OfType<RenameFieldChange>()
                .ToDictionary(c => c.NewField.FieldName, c => SourceName(diff, c.OldFieldName), StringComparer.OrdinalIgnoreCase);

        private static string SourceName(TableSchemaDiff diff, string oldFieldName)
            => diff.RealTable?.Fields?.Contains(oldFieldName) == true
                ? diff.RealTable.Fields[oldFieldName].FieldName
                : oldFieldName;

        /// <summary>
        /// Clones <paramref name="schema"/> under a different table name, used to create the
        /// temporary rebuild table.
        /// </summary>
        /// <param name="schema">The source schema.</param>
        /// <param name="tableName">The table name the clone should carry.</param>
        public static TableSchema CloneWithTableName(TableSchema schema, string tableName)
        {
            var tmpSchema = schema.Clone();
            tmpSchema.TableName = tableName;
            // `DisplayName` is carried over so the tmp table gets the same extended property. After
            // the rename the properties remain attached to the renamed object.
            tmpSchema.DisplayName = schema.DisplayName;
            return tmpSchema;
        }

        /// <summary>
        /// Removes all non-primary-key indexes from the schema. Used when generating the
        /// temp table so secondary indexes can later be created with their real names.
        /// </summary>
        /// <param name="schema">The schema to strip in place.</param>
        public static void StripNonPrimaryKeyIndexes(TableSchema schema)
        {
            var toRemove = schema.Indexes!.Where(i => !i.PrimaryKey).ToList();
            foreach (var index in toRemove)
                schema.Indexes!.Remove(index);
        }
    }
}
