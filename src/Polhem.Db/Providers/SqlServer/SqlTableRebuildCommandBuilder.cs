using System.Globalization;
using System.Text;
using Polhem.Core;
using Polhem.Db.Ddl;
using Polhem.Db.Schema;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.SqlServer
{
    /// <summary>
    /// Builds the SQL Server rebuild script (drop tmp / create tmp / copy data / drop old / rename tmp)
    /// used as the orchestrator fallback when ALTER cannot apply all changes.
    /// </summary>
    internal class SqlTableRebuildCommandBuilder : ITableRebuildCommandBuilder
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
            var createBuilder = new SqlCreateTableCommandBuilder();
            sb.AppendLine(createBuilder.GetCommandText(tmpSchema));

            // 3) Copy data from the original table (excluding newly-added and identity columns).
            sb.AppendLine("-- Move data");
            var sourceNames = RebuildSchemaFactory.GetSourceFieldNames(diff);
            var identity = DdlFragments.FindCopiedAutoIncrement(effectiveSchema, addedFieldNames);
            string insert = DdlFragments.BuildInsertSelectStatement(tableName, tmpTableName, effectiveSchema, addedFieldNames, sourceNames, SqlSchemaSyntax.QuoteName);
            if (identity == null)
            {
                sb.AppendLine(insert);
            }
            else
            {
                // The identity values are copied, then the identity carries on after the old table's last value, so a
                // number freed by a delete is not handed out again.
                string source = sourceNames.TryGetValue(identity.FieldName, out var renamed) ? renamed : identity.FieldName;
                sb.Append(BuildCaptureLastIdentity(tableName, source));
                sb.AppendLine(CultureInfo.InvariantCulture, $"SET IDENTITY_INSERT {SqlSchemaSyntax.QuoteName(tmpTableName)} ON;");
                sb.AppendLine(insert);
                sb.AppendLine(CultureInfo.InvariantCulture, $"SET IDENTITY_INSERT {SqlSchemaSyntax.QuoteName(tmpTableName)} OFF;");
                sb.Append(BuildReseed(tmpTableName, identity.FieldName));
            }

            // 4) Drop the original table.
            sb.AppendLine("-- Drop old table");
            sb.AppendLine(BuildDropIfExistsStatement(tableName));

            // 5) Rename indexes, then rename the table.
            sb.AppendLine("-- Rename temporary table");
            sb.Append(BuildRenameStatements(tmpTableName, tableName, effectiveSchema));

            return sb.ToString();
        }

        /// <summary>
        /// Reads the last identity value the old table handed out; <c>NULL</c> when it handed out none.
        /// </summary>
        private static string BuildCaptureLastIdentity(string tableName, string columnName)
        {
            string table = SqlSchemaSyntax.EscapeSqlString(SqlSchemaSyntax.QuoteName(tableName));
            string column = SqlSchemaSyntax.EscapeSqlString(columnName);
            return "DECLARE @polhem_last_identity numeric(38, 0) = (SELECT CONVERT(numeric(38, 0), last_value) " +
                   $"FROM sys.identity_columns WHERE object_id = OBJECT_ID(N'{table}') AND name = N'{column}');\n";
        }

        /// <summary>
        /// Moves the temporary table's identity past the old table's last value when that is higher than what the
        /// copy wrote.
        /// </summary>
        /// <remarks>
        /// <c>DBCC CHECKIDENT</c> takes a literal, so it runs through dynamic SQL. On a table no row was inserted into,
        /// the next value is the reseed value itself rather than the one after it.
        /// </remarks>
        private static string BuildReseed(string tmpTableName, string columnName)
        {
            string tmp = SqlSchemaSyntax.QuoteName(tmpTableName);
            string column = SqlSchemaSyntax.QuoteName(columnName);
            string tmpLiteral = SqlSchemaSyntax.EscapeSqlString(SqlSchemaSyntax.EscapeSqlString(tmp));
            // `EXEC (...)` concatenates only literals and variables, so the statement is built into a variable first.
            return $"DECLARE @polhem_reseed nvarchar(200) = N'DBCC CHECKIDENT (N''{tmpLiteral}'', RESEED, ' + CAST(@polhem_last_identity " +
                   $"+ CASE WHEN EXISTS (SELECT 1 FROM {tmp}) THEN 0 ELSE 1 END AS nvarchar(40)) + N') WITH NO_INFOMSGS';\n" +
                   $"IF @polhem_last_identity IS NOT NULL AND @polhem_last_identity > ISNULL((SELECT MAX({column}) FROM {tmp}), 0)\n" +
                   "    EXEC (@polhem_reseed);\n";
        }

        private static string BuildDropIfExistsStatement(string tableName)
        {
            string escaped = SqlSchemaSyntax.EscapeSqlString(tableName);
            string quoted = SqlSchemaSyntax.QuoteName(tableName);
            return $"IF (SELECT COUNT(*) From sys.tables WHERE name=N'{escaped}')>0\n  DROP TABLE {quoted};";
        }

        private static string BuildRenameStatements(string oldTable, string newTable, TableSchema schema)
        {
            var sb = new StringBuilder();
            // Rename indexes (including PK) so they follow the table.
            foreach (var indexName in schema.Indexes!.Select(index => index.Name))
            {
                string oldIndexName = StringUtilities.Format(indexName, oldTable);
                string newIndexName = StringUtilities.Format(indexName, newTable);
                string oldQualified = SqlSchemaSyntax.EscapeSqlString($"dbo.{oldTable}.{oldIndexName}");
                string escapedNew = SqlSchemaSyntax.EscapeSqlString(newIndexName);
                sb.Append(CultureInfo.InvariantCulture, $"EXEC sp_rename N'{oldQualified}', N'{escapedNew}', N'INDEX';\n");
            }
            // Rename the table.
            string oldEscaped = SqlSchemaSyntax.EscapeSqlString(oldTable);
            string newEscaped = SqlSchemaSyntax.EscapeSqlString(newTable);
            sb.Append(CultureInfo.InvariantCulture, $"EXEC sp_rename N'{oldEscaped}', N'{newEscaped}';\n");
            return sb.ToString();
        }
    }
}
