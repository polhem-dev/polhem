using System.Globalization;
using System.Text;
using Polhem.Core;
using Polhem.Db.Ddl;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.Sqlite
{
    /// <summary>
    /// Builds the SQLite rebuild script (drop tmp / create tmp / copy data / drop old / rename
    /// tmp / recreate indexes) used as the orchestrator fallback. SQLite has no
    /// <c>ALTER INDEX RENAME</c>, so non-PK indexes are recreated against the renamed table
    /// instead of being renamed in place. Counterpart to
    /// <see cref="PostgreSql.PgTableRebuildCommandBuilder"/>.
    /// </summary>
    internal class SqliteTableRebuildCommandBuilder : ITableRebuildCommandBuilder
    {
        /// <summary>
        /// Produces the rebuild SQL script for the given diff. Extension fields (real-only) are preserved;
        /// newly added fields are excluded from the INSERT ... SELECT data copy so existing rows get their default.
        /// </summary>
        /// <param name="diff">The schema diff; must not be a new-table diff (use <see cref="ICreateTableCommandBuilder"/> for that).</param>
        public string GetCommandText(TableSchemaDiff diff)
        {
            if (diff.IsNewTable)
                throw new InvalidOperationException("Rebuild is not applicable for a new table; use CREATE TABLE instead.");

            string tableName = diff.DefineTable.TableName;
            string tmpTableName = $"tmp_{tableName}";

            var effectiveSchema = RebuildSchemaFactory.BuildEffectiveSchema(diff);
            var addedFieldNames = diff.Changes.OfType<AddFieldChange>()
                .Select(c => c.Field.FieldName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"-- Rebuild table {tableName}");

            // 1) Drop any leftover temp table from a prior failed run.
            sb.AppendLine("-- Drop temporary table");
            sb.AppendLine(BuildDropIfExistsStatement(tmpTableName));

            // 2) Create the temp table with only the primary key (no secondary indexes).
            //    Secondary indexes are recreated in step 6 with their real names against the
            //    final table — SQLite cannot rename indexes.
            sb.AppendLine("-- Create temporary table");
            var tmpSchema = RebuildSchemaFactory.CloneWithTableName(effectiveSchema, tmpTableName);
            RebuildSchemaFactory.StripNonPrimaryKeyIndexes(tmpSchema);
            var createBuilder = new SqliteCreateTableCommandBuilder();
            sb.AppendLine(createBuilder.GetCommandText(tmpSchema));

            // 3) Copy data from the original table (excluding newly-added columns). Auto-increment values are
            //    copied too, and the table's sequence carried over, so `sys_no` keeps its numbers and a number
            //    freed by a delete is not handed out again.
            sb.AppendLine("-- Move data");
            sb.AppendLine(DdlFragments.BuildInsertSelectStatement(tableName, tmpTableName, effectiveSchema, addedFieldNames,
                RebuildSchemaFactory.GetSourceFieldNames(diff), SqliteSchemaSyntax.QuoteName));
            if (DdlFragments.FindCopiedAutoIncrement(effectiveSchema, addedFieldNames) != null)
                sb.Append(BuildCarrySequenceStatements(tableName, tmpTableName));

            // 4) Drop the original table — this also drops every index and the autoindex backing
            //    its primary key, freeing those names for recreation in step 6.
            sb.AppendLine("-- Drop old table");
            sb.AppendLine(BuildDropIfExistsStatement(tableName));

            // 5) Rename the temp table to the original name. SQLite carries indexes through the
            //    rename, so the temporary table's autoindex PK is preserved (its name is opaque).
            sb.AppendLine("-- Rename temporary table");
            sb.AppendLine(BuildRenameTableStatement(tmpTableName, tableName));

            // 6) Recreate each non-PK index, the database-only ones included, on the renamed table.
            sb.Append(BuildRecreateIndexStatements(tableName,
                effectiveSchema.Indexes!.Concat(RebuildSchemaFactory.GetDatabaseOnlyIndexes(diff))));

            return sb.ToString();
        }

        /// <summary>
        /// Carries the source table's <c>AUTOINCREMENT</c> sequence over to the temporary table, keeping the higher
        /// of the two. Copying the rows sets the temporary table's sequence to the highest copied value only, which
        /// is lower than the source's when its last rows were deleted.
        /// </summary>
        private static string BuildCarrySequenceStatements(string tableName, string tmpTableName)
        {
            string source = $"'{SqliteSchemaSyntax.EscapeSqlString(tableName)}'";
            string target = $"'{SqliteSchemaSyntax.EscapeSqlString(tmpTableName)}'";
            return $"INSERT INTO sqlite_sequence (name, seq) SELECT {target}, seq FROM sqlite_sequence WHERE name = {source} " +
                   $"AND NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = {target});\n" +
                   $"UPDATE sqlite_sequence SET seq = MAX(seq, COALESCE((SELECT seq FROM sqlite_sequence WHERE name = {source}), 0)) " +
                   $"WHERE name = {target};\n";
        }

        private static string BuildDropIfExistsStatement(string tableName)
        {
            return $"DROP TABLE IF EXISTS {SqliteSchemaSyntax.QuoteName(tableName)};";
        }

        private static string BuildRenameTableStatement(string oldTable, string newTable)
        {
            return $"ALTER TABLE {SqliteSchemaSyntax.QuoteName(oldTable)} RENAME TO {SqliteSchemaSyntax.QuoteName(newTable)};";
        }

        private static string BuildRecreateIndexStatements(string tableName, IEnumerable<DbTableIndex> indexes)
        {
            var sb = new StringBuilder();
            foreach (var index in indexes.Where(i => !i.PrimaryKey))
            {
                string name = StringUtilities.Format(index.Name, tableName);
                string fields = DdlFragments.BuildIndexFieldList(index, SqliteSchemaSyntax.QuoteName);
                string uniqueClause = index.Unique ? "UNIQUE " : string.Empty;
                sb.Append(CultureInfo.InvariantCulture,
                    $"CREATE {uniqueClause}INDEX {SqliteSchemaSyntax.QuoteName(name)} ON {SqliteSchemaSyntax.QuoteName(tableName)} ({fields});\n");
            }
            return sb.ToString();
        }
    }
}
