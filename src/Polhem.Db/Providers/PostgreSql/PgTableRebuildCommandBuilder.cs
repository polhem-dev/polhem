using System.Globalization;
using System.Text;
using Polhem.Core;
using Polhem.Db.Ddl;
using Polhem.Db.Schema;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.PostgreSql
{
    /// <summary>
    /// Builds the PostgreSQL rebuild script (drop tmp / create tmp / copy data / drop old / rename tmp)
    /// used as the orchestrator fallback when ALTER cannot apply all changes. Counterpart to
    /// <see cref="SqlServer.SqlTableRebuildCommandBuilder"/>.
    /// </summary>
    internal class PgTableRebuildCommandBuilder : ITableRebuildCommandBuilder
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
            // Columns the rebuild adds, or numbers afresh, are left to their defaults rather than copied.
            var addedFieldNames = RebuildSchemaFactory.GetUncopiedFieldNames(diff);

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"-- Rebuild table {tableName}");

            // 1) Drop any leftover temp table from a prior failed run.
            sb.AppendLine("-- Drop temporary table");
            sb.AppendLine(BuildDropIfExistsStatement(tmpTableName));

            // 2) Create the temp table using the CREATE builder (with the tmp name).
            sb.AppendLine("-- Create temporary table");
            var tmpSchema = RebuildSchemaFactory.CloneWithTableName(effectiveSchema, tmpTableName);
            var createBuilder = new PgCreateTableCommandBuilder();
            sb.AppendLine(createBuilder.GetCommandText(tmpSchema));

            // 3) Copy data from the original table (excluding newly-added and identity columns).
            sb.AppendLine("-- Move data");
            var sourceNames = RebuildSchemaFactory.GetSourceFieldNames(diff);
            sb.AppendLine(DdlFragments.BuildInsertSelectStatement(tableName, tmpTableName, effectiveSchema, addedFieldNames, sourceNames, PgSchemaSyntax.QuoteName));
            var identity = DdlFragments.FindCopiedAutoIncrement(effectiveSchema, addedFieldNames);
            if (identity != null)
            {
                string source = sourceNames.TryGetValue(identity.FieldName, out var renamed) ? renamed : identity.FieldName;
                sb.AppendLine(BuildCarrySequence(tableName, source, tmpTableName, identity.FieldName));
            }

            // 4) Drop the original table.
            sb.AppendLine("-- Drop old table");
            sb.AppendLine(BuildDropIfExistsStatement(tableName));

            // 5) Rename indexes (PG: ALTER INDEX), then rename the table.
            sb.AppendLine("-- Rename temporary table");
            sb.Append(BuildRenameStatements(tmpTableName, tableName, effectiveSchema));

            return sb.ToString();
        }

        /// <summary>
        /// Moves the temporary table's identity sequence past both the copied values and the old sequence's last
        /// value, so a number freed by a delete is not handed out again.
        /// </summary>
        /// <remarks>
        /// <c>pg_get_serial_sequence</c> is <c>NULL</c> when the old column was not an identity, and
        /// <c>pg_sequence_last_value</c> is <c>NULL</c> when its sequence never handed out a value.
        /// </remarks>
        private static string BuildCarrySequence(string tableName, string sourceColumn, string tmpTableName, string column)
        {
            string old = PgSchemaSyntax.EscapeSqlString(PgSchemaSyntax.QuoteName(tableName));
            string tmp = PgSchemaSyntax.EscapeSqlString(PgSchemaSyntax.QuoteName(tmpTableName));
            string next = $"GREATEST(COALESCE(pg_sequence_last_value(pg_get_serial_sequence('{old}', '{PgSchemaSyntax.EscapeSqlString(sourceColumn)}')::regclass), 0), " +
                          $"COALESCE((SELECT MAX({PgSchemaSyntax.QuoteName(column)}) FROM {PgSchemaSyntax.QuoteName(tmpTableName)}), 0))";
            return $"SELECT setval(pg_get_serial_sequence('{tmp}', '{PgSchemaSyntax.EscapeSqlString(column)}'), GREATEST(v, 1), v > 0) " +
                   $"FROM (SELECT {next} AS v) AS last_identity;";
        }

        private static string BuildDropIfExistsStatement(string tableName)
        {
            return $"DROP TABLE IF EXISTS {PgSchemaSyntax.QuoteName(tableName)};";
        }

        private static string BuildRenameStatements(string oldTable, string newTable, TableSchema schema)
        {
            var sb = new StringBuilder();
            // Rename indexes (and the PK constraint, which is also exposed as an index in PG) so they follow the table.
            foreach (var indexName in schema.Indexes!.Select(index => index.Name))
            {
                string oldIndexName = StringUtilities.Format(indexName, oldTable);
                string newIndexName = StringUtilities.Format(indexName, newTable);
                sb.Append(CultureInfo.InvariantCulture,
                    $"ALTER INDEX {PgSchemaSyntax.QuoteName(oldIndexName)} RENAME TO {PgSchemaSyntax.QuoteName(newIndexName)};\n");
            }
            // Rename the table.
            sb.Append(CultureInfo.InvariantCulture,
                $"ALTER TABLE {PgSchemaSyntax.QuoteName(oldTable)} RENAME TO {PgSchemaSyntax.QuoteName(newTable)};\n");
            return sb.ToString();
        }
    }
}
