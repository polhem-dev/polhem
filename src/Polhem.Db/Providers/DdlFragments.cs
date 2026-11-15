using System.Globalization;
using System.Text;
using Polhem.Core.Data;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers
{
    /// <summary>
    /// DDL fragments that every dialect builds the same way apart from identifier quoting.
    /// </summary>
    /// <remarks>
    /// These were byte-identical copies in the providers' alter and rebuild builders, differing only
    /// in which <c>QuoteName</c> they called. The dialect passes its own quoting, so a rule that is
    /// not dialect-specific, such as which columns a rebuild copies, is fixed once for all of them.
    /// </remarks>
    internal static class DdlFragments
    {
        /// <summary>
        /// Builds the comma-separated column list of an index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="quoteName">The dialect's identifier quoting.</param>
        /// <param name="includeSortDirection">
        /// Whether each column carries <c>ASC</c> / <c>DESC</c>. Dialects that reject a sort direction
        /// inside a <c>PRIMARY KEY</c> constraint pass <c>false</c> for it.
        /// </param>
        public static string BuildIndexFieldList(DbTableIndex index, Func<string, string> quoteName, bool includeSortDirection = true)
        {
            var sb = new StringBuilder();
            foreach (IndexField field in index.IndexFields!)
            {
                if (sb.Length > 0) sb.Append(", ");
                if (includeSortDirection)
                {
                    sb.Append(CultureInfo.InvariantCulture,
                        $"{quoteName(field.FieldName)} {field.SortDirection.ToString().ToUpperInvariant()}");
                }
                else
                {
                    sb.Append(quoteName(field.FieldName));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns the auto-increment column the rebuild copies, or <c>null</c> when the table has none or the rebuild
        /// adds it.
        /// </summary>
        /// <param name="schema">The schema of the rebuilt table.</param>
        /// <param name="addedFieldNames">Columns the rebuild adds.</param>
        public static DbField? FindCopiedAutoIncrement(TableSchema schema, HashSet<string> addedFieldNames)
            => schema.Fields!.FirstOrDefault(f => f.DbType == FieldDbType.AutoIncrement && !addedFieldNames.Contains(f.FieldName));

        /// <summary>
        /// Builds the <c>INSERT INTO ... SELECT</c> that copies the existing rows into the rebuilt table.
        /// </summary>
        /// <param name="sourceTable">The table being rebuilt.</param>
        /// <param name="targetTable">The temporary table that replaces it.</param>
        /// <param name="schema">The schema of the rebuilt table.</param>
        /// <param name="addedFieldNames">
        /// Columns the rebuild adds. They do not exist in the source table, so they are left to their defaults.
        /// </param>
        /// <param name="sourceFieldNames">
        /// The source column of each renamed field, keyed by its new name; other fields are copied from the column of
        /// the same name.
        /// </param>
        /// <param name="quoteName">The dialect's identifier quoting.</param>
        /// <remarks>
        /// Auto-increment columns are copied with their values, so a rebuild does not renumber the rows; each dialect
        /// lets the copy write them (<see cref="FindCopiedAutoIncrement"/>) and moves its sequence on afterwards.
        /// </remarks>
        public static string BuildInsertSelectStatement(string sourceTable, string targetTable, TableSchema schema,
            HashSet<string> addedFieldNames, IReadOnlyDictionary<string, string> sourceFieldNames,
            Func<string, string> quoteName)
        {
            var copied = schema.Fields!.Select(f => f.FieldName).Where(name => !addedFieldNames.Contains(name)).ToList();
            string targetFields = string.Join(", ", copied.Select(quoteName));
            string sourceFields = string.Join(", ", copied.Select(name =>
                quoteName(sourceFieldNames.TryGetValue(name, out var source) ? source : name)));
            return $"INSERT INTO {quoteName(targetTable)} ({targetFields}) \nSELECT {sourceFields} FROM {quoteName(sourceTable)};";
        }
    }
}
