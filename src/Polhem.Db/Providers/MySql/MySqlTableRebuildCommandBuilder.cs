using System.Globalization;
using System.Text;
using Polhem.Base;
using Polhem.Db.Ddl;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.MySql
{
    /// <summary>
    /// Builds the MySQL 8.0+ rebuild script (drop tmp / create tmp / copy data / drop old /
    /// rename tmp / recreate indexes) used as the orchestrator fallback when in-place ALTER
    /// cannot apply all changes. Counterpart to
    /// <see cref="Sqlite.SqliteTableRebuildCommandBuilder"/> and
    /// <see cref="PostgreSql.PgTableRebuildCommandBuilder"/>.
    /// </summary>
    /// <remarks>
    /// MySQL 8.0+ natively supports most schema changes via <c>ALTER TABLE</c>, so this
    /// path is rarely invoked — typically only for cross-family type changes flagged by
    /// <see cref="Schema.AlterCompatibilityRules"/>. The pattern mirrors SQLite: temp table
    /// is created with PK only, secondary indexes are recreated against the renamed table
    /// (avoids the need to track per-step index renames). New fields are excluded from
    /// the data copy so existing rows pick up their default; identity columns are also
    /// excluded so MySQL re-allocates AUTO_INCREMENT values cleanly.
    /// </remarks>
    internal class MySqlTableRebuildCommandBuilder : ITableRebuildCommandBuilder
    {
        /// <summary>
        /// Produces the rebuild SQL script for the given diff. Extension fields (real-only)
        /// are preserved; newly added fields are excluded from the INSERT ... SELECT data
        /// copy so existing rows get their default.
        /// </summary>
        /// <param name="diff">The schema diff; must not be a new-table diff (use
        /// <see cref="ICreateTableCommandBuilder"/> for that).</param>
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
            //    final table, avoiding the need to use ALTER TABLE ... RENAME INDEX.
            sb.AppendLine("-- Create temporary table");
            var tmpSchema = RebuildSchemaFactory.CloneWithTableName(effectiveSchema, tmpTableName);
            RebuildSchemaFactory.StripNonPrimaryKeyIndexes(tmpSchema);
            var createBuilder = new MySqlCreateTableCommandBuilder();
            sb.AppendLine(createBuilder.GetCommandText(tmpSchema));

            // 3) Copy data from the original table (excluding newly-added and identity columns).
            sb.AppendLine("-- Move data");
            sb.AppendLine(DdlFragments.BuildInsertSelectStatement(tableName, tmpTableName, effectiveSchema, addedFieldNames, MySqlSchemaSyntax.QuoteName));

            // 4) Drop the original table — this also drops every index and the auto-generated
            //    primary-key index, freeing those names for recreation in step 6.
            sb.AppendLine("-- Drop old table");
            sb.AppendLine(BuildDropIfExistsStatement(tableName));

            // 5) Rename the temp table to the original name. MySQL carries indexes through
            //    the rename, so the temporary table's PRIMARY KEY is preserved.
            sb.AppendLine("-- Rename temporary table");
            sb.AppendLine(BuildRenameTableStatement(tmpTableName, tableName));

            // 6) Recreate each non-PK index on the renamed table with its canonical name.
            sb.Append(BuildRecreateIndexStatements(tableName, effectiveSchema));

            return sb.ToString();
        }

        private static string BuildDropIfExistsStatement(string tableName)
        {
            return $"DROP TABLE IF EXISTS {MySqlSchemaSyntax.QuoteName(tableName)};";
        }

        private static string BuildRenameTableStatement(string oldTable, string newTable)
        {
            return $"ALTER TABLE {MySqlSchemaSyntax.QuoteName(oldTable)} RENAME TO {MySqlSchemaSyntax.QuoteName(newTable)};";
        }

        private static string BuildRecreateIndexStatements(string tableName, TableSchema schema)
        {
            var sb = new StringBuilder();
            foreach (var index in schema.Indexes!.Where(i => !i.PrimaryKey))
            {
                string name = StringUtilities.Format(index.Name, tableName);
                string fields = DdlFragments.BuildIndexFieldList(index, MySqlSchemaSyntax.QuoteName);
                string uniqueClause = index.Unique ? "UNIQUE " : string.Empty;
                sb.Append(CultureInfo.InvariantCulture,
                    $"CREATE {uniqueClause}INDEX {MySqlSchemaSyntax.QuoteName(name)} ON {MySqlSchemaSyntax.QuoteName(tableName)} ({fields});\n");
            }
            return sb.ToString();
        }
    }
}
