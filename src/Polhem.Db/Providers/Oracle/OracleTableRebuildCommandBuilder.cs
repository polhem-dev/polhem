using System.Globalization;
using System.Text;
using Polhem.Core;
using Polhem.Db.Ddl;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers.Oracle
{
    /// <summary>
    /// Builds the Oracle 19c+ rebuild script (drop tmp / create tmp / copy data / drop old /
    /// rename tmp / recreate indexes) used as the orchestrator fallback when in-place ALTER
    /// cannot apply all changes. Counterpart to
    /// <see cref="MySql.MySqlTableRebuildCommandBuilder"/> and
    /// <see cref="Sqlite.SqliteTableRebuildCommandBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Oracle 19c+ supports most schema changes via <c>ALTER TABLE ... MODIFY</c>, so this
    /// path is rarely invoked — typically for cross-family type changes flagged by
    /// <see cref="Schema.AlterCompatibilityRules"/>, <c>NUMBER</c> precision reduction with
    /// non-empty data, or IDENTITY add/remove. The pattern mirrors MySQL: temp table is
    /// created with PK only, secondary indexes are recreated against the renamed table.
    /// New fields and IDENTITY columns are excluded from the data copy so existing rows pick
    /// up their default and Oracle re-allocates IDENTITY values cleanly.
    /// </remarks>
    /// <para>
    /// Differences from MySQL: Oracle has no <c>DROP TABLE IF EXISTS</c>, so each drop is
    /// wrapped in a PL/SQL anonymous block that suppresses ORA-00942 (table or view does
    /// not exist). <c>CASCADE CONSTRAINTS</c> is appended to <c>DROP TABLE</c> so FKs
    /// referencing the table are removed atomically.
    /// </para>
    internal class OracleTableRebuildCommandBuilder : ITableRebuildCommandBuilder
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

            // Oracle.ManagedDataAccess runs one statement per command, and the orchestrator sends the rebuild as one.
            // The whole script is therefore a single anonymous block: the drop and create blocks nest in it, and the
            // plain statements run through EXECUTE IMMEDIATE, since the temporary table they name does not exist
            // when the block is compiled.
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"-- Rebuild table {tableName}");
            sb.AppendLine("BEGIN");

            // 1) Drop any leftover temp table from a prior failed run.
            sb.AppendLine("-- Drop temporary table");
            sb.AppendLine(BuildDropIfExistsStatement(tmpTableName));

            // 2) Create the temp table with only the primary key (no secondary indexes).
            //    Secondary indexes are recreated in step 6 with their real names against the
            //    final table, avoiding the need to use ALTER INDEX ... RENAME.
            sb.AppendLine("-- Create temporary table");
            var tmpSchema = RebuildSchemaFactory.CloneWithTableName(effectiveSchema, tmpTableName);
            RebuildSchemaFactory.StripNonPrimaryKeyIndexes(tmpSchema);
            var createBuilder = new OracleCreateTableCommandBuilder();
            sb.AppendLine(createBuilder.GetCommandText(tmpSchema));

            // 3) Copy data from the original table (excluding newly-added and identity columns).
            sb.AppendLine("-- Move data");
            var sourceNames = RebuildSchemaFactory.GetSourceFieldNames(diff);
            sb.AppendLine(Dynamic(DdlFragments.BuildInsertSelectStatement(tableName, tmpTableName, effectiveSchema, addedFieldNames, sourceNames, OracleSchemaSyntax.QuoteName)));
            var identity = DdlFragments.FindCopiedAutoIncrement(effectiveSchema, addedFieldNames);
            if (identity != null)
            {
                string source = sourceNames.TryGetValue(identity.FieldName, out var renamed) ? renamed : identity.FieldName;
                sb.Append(BuildRestartIdentity(tableName, source, tmpTableName, identity.FieldName));
            }

            // 4) Drop the original table — CASCADE CONSTRAINTS removes referencing FKs and
            //    drops every index / auto-named PK constraint, freeing those names for
            //    recreation in step 6.
            sb.AppendLine("-- Drop old table");
            sb.AppendLine(BuildDropIfExistsStatement(tableName));

            // 5) Rename the temp table to the original name. Oracle preserves the PK
            //    constraint and indexes through ALTER TABLE ... RENAME TO.
            sb.AppendLine("-- Rename temporary table");
            sb.AppendLine(Dynamic(BuildRenameTableStatement(tmpTableName, tableName)));

            // 6) Recreate each non-PK index, the database-only ones included, on the renamed table.
            sb.Append(BuildRecreateIndexStatements(tableName,
                effectiveSchema.Indexes!.Concat(RebuildSchemaFactory.GetDatabaseOnlyIndexes(diff))));

            sb.Append("END;");
            return sb.ToString();
        }

        /// <summary>
        /// Wraps a plain statement in <c>EXECUTE IMMEDIATE</c> for the rebuild block, dropping its terminator.
        /// </summary>
        private static string Dynamic(string statement)
            => $"  EXECUTE IMMEDIATE '{OracleSchemaSyntax.EscapeSqlString(statement.TrimEnd().TrimEnd(';'))}';";

        /// <summary>
        /// Builds an Oracle "drop-if-exists" equivalent: a PL/SQL anonymous block that
        /// catches ORA-00942 (table or view does not exist) and rethrows anything else.
        /// </summary>
        /// <remarks>
        /// Oracle has no <c>DROP TABLE IF EXISTS</c> syntax. The conventional alternative
        /// is to query <c>ALL_TABLES</c> first, but the PL/SQL block keeps the rebuild
        /// script as a single self-contained sequence with no dictionary-lookup round trips.
        /// <c>CASCADE CONSTRAINTS</c> ensures referencing FKs are removed.
        /// </remarks>
        /// <summary>
        /// Restarts the temporary table's identity after both the copied values and the old identity's sequence, so
        /// the next row neither collides with a copied one nor takes a number freed by a delete.
        /// </summary>
        /// <remarks>
        /// <c>LAST_NUMBER</c> is the sequence's cached high-water mark, so a few numbers may be skipped; none is reused.
        /// </remarks>
        private static string BuildRestartIdentity(string tableName, string sourceColumn, string tmpTableName, string column)
        {
            string oldTable = OracleSchemaSyntax.EscapeSqlString(tableName.ToUpperInvariant());
            string oldColumn = OracleSchemaSyntax.EscapeSqlString(sourceColumn.ToUpperInvariant());
            string quotedColumn = OracleSchemaSyntax.QuoteName(column);
            string quotedTmp = OracleSchemaSyntax.QuoteName(tmpTableName);
            string selectMax = OracleSchemaSyntax.EscapeSqlString($"SELECT NVL(MAX({quotedColumn}), 0) FROM {quotedTmp}");
            string alter = OracleSchemaSyntax.EscapeSqlString($"ALTER TABLE {quotedTmp} MODIFY ({quotedColumn} GENERATED BY DEFAULT AS IDENTITY (START WITH ");
            return "  DECLARE\n" +
                   "    v_old NUMBER;\n" +
                   "    v_max NUMBER;\n" +
                   "  BEGIN\n" +
                   "    SELECT NVL(MAX(s.LAST_NUMBER), 0) INTO v_old FROM USER_TAB_IDENTITY_COLS c " +
                   "JOIN USER_SEQUENCES s ON s.SEQUENCE_NAME = c.SEQUENCE_NAME " +
                   $"WHERE c.TABLE_NAME = '{oldTable}' AND c.COLUMN_NAME = '{oldColumn}';\n" +
                   $"    EXECUTE IMMEDIATE '{selectMax}' INTO v_max;\n" +
                   $"    EXECUTE IMMEDIATE '{alter}' || TO_CHAR(GREATEST(v_old, v_max + 1)) || '))';\n" +
                   "  END;\n";
        }

        private static string BuildDropIfExistsStatement(string tableName)
        {
            // The statement sits inside a string literal, so a quote in the name is doubled once more.
            string quoted = OracleSchemaSyntax.EscapeSqlString(OracleSchemaSyntax.QuoteName(tableName));
            return "BEGIN\r\n" +
                   $"  EXECUTE IMMEDIATE 'DROP TABLE {quoted} CASCADE CONSTRAINTS';\r\n" +
                   "EXCEPTION\r\n" +
                   "  WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF;\r\n" +
                   "END;";
        }

        private static string BuildRenameTableStatement(string oldTable, string newTable)
        {
            return $"ALTER TABLE {OracleSchemaSyntax.QuoteName(oldTable)} RENAME TO {OracleSchemaSyntax.QuoteName(newTable)};";
        }

        private static string BuildRecreateIndexStatements(string tableName, IEnumerable<DbTableIndex> indexes)
        {
            var sb = new StringBuilder();
            foreach (var index in indexes.Where(i => !i.PrimaryKey))
            {
                string name = StringUtilities.Format(index.Name, tableName);
                string fields = DdlFragments.BuildIndexFieldList(index, OracleSchemaSyntax.QuoteName);
                string uniqueClause = index.Unique ? "UNIQUE " : string.Empty;
                sb.Append(CultureInfo.InvariantCulture,
                    $"{Dynamic($"CREATE {uniqueClause}INDEX {OracleSchemaSyntax.QuoteName(name)} ON {OracleSchemaSyntax.QuoteName(tableName)} ({fields})")}\n");
            }
            return sb.ToString();
        }
    }
}
