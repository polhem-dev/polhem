using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Sorting;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// Checks the field names a SELECT references against the form table it reads.
    /// </summary>
    /// <remarks>
    /// Quoting keeps a caller-supplied name from injecting SQL, but it does not keep the name inside
    /// the form: any physical column of the table would otherwise be reachable through a filter or a
    /// sort, including columns the form deliberately leaves out. A column that can be compared can be
    /// read one comparison at a time, so filter and sort names are held to the same rule as the select
    /// list. <see cref="DeleteCommandBuilder"/> applies the equivalent rule to DELETE filters.
    /// </remarks>
    internal static class SelectFieldGuard
    {
        /// <summary>
        /// Throws when a filter or sort names a field the form table does not declare, a field with no
        /// source to read from, or a protected column.
        /// </summary>
        /// <param name="formTable">The form table the query reads.</param>
        /// <param name="filter">The filter condition; may be <c>null</c>.</param>
        /// <param name="sortFields">The sort fields; may be <c>null</c>.</param>
        /// <param name="selectContext">The field source mappings built for the query.</param>
        /// <exception cref="InvalidOperationException">A referenced field is not allowed.</exception>
        public static void ValidateFilterAndSort(
            FormTable formTable, FilterNode? filter, SortFieldCollection? sortFields, SelectContext selectContext)
        {
            foreach (var fieldName in FilterFieldNames(filter))
                RequireQueryable(formTable, fieldName, selectContext, "Filter");

            if (sortFields == null) { return; }
            foreach (var sortField in sortFields)
                RequireQueryable(formTable, sortField.FieldName, selectContext, "Sort");
        }

        /// <summary>
        /// Throws when a field named in the select list is a protected column.
        /// </summary>
        /// <param name="formTable">The form table the query reads.</param>
        /// <param name="field">The declared field.</param>
        /// <param name="selectContext">The field source mappings built for the query.</param>
        /// <exception cref="InvalidOperationException">The field is protected.</exception>
        public static void RequireSelectable(FormTable formTable, FormField field, SelectContext selectContext)
        {
            if (IsProtected(formTable, field, selectContext))
                throw new InvalidOperationException($"Select cannot reference the protected field '{field.FieldName}'.");
        }

        /// <summary>
        /// Determines whether a declared field reads a <see cref="ProtectedFields"/> column, either
        /// directly or, for a relation field, through the related table.
        /// </summary>
        /// <param name="formTable">The form table the query reads.</param>
        /// <param name="field">The declared field.</param>
        /// <param name="selectContext">The field source mappings built for the query.</param>
        public static bool IsProtected(FormTable formTable, FormField field, SelectContext selectContext)
        {
            if (field.Type == FieldType.DbField)
                return ProtectedFields.IsProtected(DbTableNameOf(formTable), field.FieldName);

            var mapping = selectContext.FieldMappings.GetOrDefault(field.FieldName);
            return mapping?.TableJoin != null
                && ProtectedFields.IsProtected(mapping.TableJoin.RightTable, mapping.SourceField);
        }

        private static void RequireQueryable(FormTable formTable, string fieldName, SelectContext selectContext, string usage)
        {
            var field = formTable.Fields!.GetOrDefault(fieldName)
                ?? throw new InvalidOperationException(
                    $"{usage} references the field '{fieldName}', which table '{formTable.TableName}' does not declare.");

            // A field that is neither a physical column nor resolved through a relation has nothing to
            // read from. The WHERE and ORDER BY builders would fall back to a main-table column of that
            // name, which is exactly the undeclared access this guard exists to refuse.
            bool hasSource = field.Type == FieldType.DbField
                || selectContext.FieldMappings.GetOrDefault(fieldName) != null;
            if (!hasSource)
            {
                throw new InvalidOperationException(
                    $"{usage} references the {field.Type} '{fieldName}', which has no column to query.");
            }

            if (IsProtected(formTable, field, selectContext))
                throw new InvalidOperationException($"{usage} cannot reference the protected field '{fieldName}'.");
        }

        private static IEnumerable<string> FilterFieldNames(FilterNode? node)
        {
            if (node is FilterCondition condition)
            {
                yield return condition.FieldName;
            }
            else if (node is FilterGroup group)
            {
                foreach (var name in group.Nodes.SelectMany(FilterFieldNames))
                    yield return name;
            }
        }

        private static string DbTableNameOf(FormTable formTable)
            => string.IsNullOrWhiteSpace(formTable.DbTableName) ? formTable.TableName : formTable.DbTableName;
    }
}
