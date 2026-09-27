using System.Globalization;
using System.Text;
using Polhem.Base.Data;
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
        /// Builds the <c>INSERT INTO ... SELECT</c> that copies the existing rows into the rebuilt table.
        /// </summary>
        /// <param name="sourceTable">The table being rebuilt.</param>
        /// <param name="targetTable">The temporary table that replaces it.</param>
        /// <param name="schema">The schema of the rebuilt table.</param>
        /// <param name="addedFieldNames">
        /// Columns the rebuild adds. They do not exist in the source table, so they are left to their defaults.
        /// </param>
        /// <param name="quoteName">The dialect's identifier quoting.</param>
        /// <remarks>
        /// Auto-increment columns are not copied: the target generates its own values.
        /// </remarks>
        public static string BuildInsertSelectStatement(string sourceTable, string targetTable, TableSchema schema,
            HashSet<string> addedFieldNames, Func<string, string> quoteName)
        {
            var fieldBuilder = new StringBuilder();
            foreach (DbField field in schema.Fields!)
            {
                if (addedFieldNames.Contains(field.FieldName)) continue;
                if (field.DbType == FieldDbType.AutoIncrement) continue;
                if (fieldBuilder.Length > 0) fieldBuilder.Append(", ");
                fieldBuilder.Append(quoteName(field.FieldName));
            }
            string fields = fieldBuilder.ToString();
            return $"INSERT INTO {quoteName(targetTable)} ({fields}) \nSELECT {fields} FROM {quoteName(sourceTable)};";
        }
    }
}
