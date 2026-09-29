using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Db.Dml;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Multi-row writes through <see cref="DbAccess.UpdateDataTables"/> on each provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ApplySpec</c> enables <c>UpdateBatchSize</c> when the provider supports it, turning one round trip per row
    /// into one per batch. Batching changes how the adapter sends commands, and the two things most likely to break are
    /// <b>the affected row count returned</b> and <b>the exception on failure</b>; both can only be checked against
    /// each real database.
    /// </para>
    /// <para>
    /// These tests fill a coverage gap: before them, the database tests of the adapter write path covered
    /// <b>SQLite only</b>, and SQLite happens to be one of the two providers without batching. The three providers
    /// where batching actually takes effect (SQL Server / MySQL / Oracle) had zero coverage of this path.
    /// </para>
    /// <para>
    /// One <c>[DbFact]</c> per provider, each skipped when its container is not running. The row count is deliberately
    /// greater than 1; otherwise batched and unbatched runs behave the same, and the test would prove nothing.
    /// </para>
    /// <para>
    /// <b>These tests guard "batching does not change the result", not "batching is on"</b>: they stay green with
    /// <c>TryEnableBatching</c> switched off, and that is deliberate, since unchanged behavior is the acceptance
    /// criterion. The batching capability itself is pinned by <c>ProviderBatchingSupportTests</c>, and the actual
    /// benefit is measured; neither lives here.
    /// </para>
    /// </remarks>
    public class UpdateDataTablesBatchingTests : IClassFixture<SharedDbFixture>
    {
        /// <summary>The number of rows written at once; it must be greater than 1 to actually reach batching.</summary>
        private const int RowCount = 5;

        private readonly SharedDbFixture _fx;

        public UpdateDataTablesBatchingTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server (supports batching) lands multi-row Added / Modified / Deleted and reports the row counts")]
        public void SqlServer_MultiRow_AppliesAndReportsAffected() => RunFor(DatabaseType.SQLServer);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL (supports batching) lands multi-row Added / Modified / Deleted and reports the row counts")]
        public void MySql_MultiRow_AppliesAndReportsAffected() => RunFor(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle (supports batching) lands multi-row Added / Modified / Deleted and reports the row counts")]
        public void Oracle_MultiRow_AppliesAndReportsAffected() => RunFor(DatabaseType.Oracle);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL (no batching, falls back to row by row) gives the same multi-row write results as the batching providers")]
        public void PostgreSql_MultiRow_AppliesAndReportsAffected() => RunFor(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite (no batching, falls back to row by row) gives the same multi-row write results as the batching providers")]
        public void Sqlite_MultiRow_AppliesAndReportsAffected() => RunFor(DatabaseType.SQLite);

        /// <summary>
        /// Runs one round of multi-row writes on the given database.
        /// </summary>
        /// <param name="databaseType">The target database.</param>
        private void RunFor(DatabaseType databaseType)
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(databaseType));

            // Oracle has a stricter identifier length limit, so the table name stays within 30 characters.
            string table = "tb_udb_" + Guid.NewGuid().ToString("N")[..8];
            string qt = databaseType.QuoteIdentifier(table);
            string qRowId = databaseType.QuoteIdentifier(SysFields.RowId);
            string qName = databaseType.QuoteIdentifier("name");

            dbAccess.ExecuteNonQuery(CreateTableSql(databaseType, qt, qRowId, qName));
            try
            {
                var schema = new TableSchema { TableName = table };
                schema.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.String, 50);
                schema.Fields!.Add("name", "Name", FieldDbType.String, 50);

                // --- 1. Multiple Added rows ---
                var added = NewTable(table);
                var ids = new string[RowCount];
                for (int i = 0; i < RowCount; i++)
                {
                    ids[i] = Guid.NewGuid().ToString("N");
                    added.Rows.Add(ids[i], $"row-{i}");
                }
                var counts = dbAccess.UpdateDataTables(
                    [new TableSchemaCommandBuilder(databaseType, schema).BuildUpdateSpec(added)]);

                // Batching changes how commands are sent, but must not change the reported row count.
                Assert.Equal(RowCount, Assert.Single(counts));
                Assert.Equal(RowCount, CountRows(dbAccess, qt));

                // --- 2. Multiple Modified rows + one Deleted row ---
                var loaded = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable,
                    $"SELECT {qRowId},{qName} FROM {qt}")).Table!;
                loaded.TableName = table;
                loaded.AcceptChanges();

                int modified = 0;
                foreach (DataRow row in loaded.Rows)
                {
                    var id = Convert.ToString(row[SysFields.RowId], CultureInfo.InvariantCulture);
                    if (id == ids[0]) { row.Delete(); }
                    else { row["name"] = "changed"; modified++; }
                }

                counts = dbAccess.UpdateDataTables(
                    [new TableSchemaCommandBuilder(databaseType, schema).BuildUpdateSpec(loaded)]);

                Assert.Equal(modified + 1, Assert.Single(counts));
                Assert.Equal(RowCount - 1, CountRows(dbAccess, qt));

                int changed = Convert.ToInt32(dbAccess.ExecuteScalar(
                    $"SELECT COUNT(*) FROM {qt} WHERE {qName}={{0}}", "changed"), CultureInfo.InvariantCulture);
                Assert.Equal(modified, changed);
            }
            finally
            {
                dbAccess.ExecuteNonQuery($"DROP TABLE {qt}");
            }
        }

        private static DataTable NewTable(string tableName)
        {
            var t = new DataTable(tableName);
            t.Columns.Add(SysFields.RowId, typeof(string));
            t.Columns.Add("name", typeof(string));
            return t;
        }

        private static int CountRows(DbAccess dbAccess, string quotedTable)
            => Convert.ToInt32(dbAccess.ExecuteScalar($"SELECT COUNT(*) FROM {quotedTable}"), CultureInfo.InvariantCulture);

        /// <summary>
        /// The CREATE TABLE statement for each dialect.
        /// </summary>
        /// <remarks>
        /// Written by hand on purpose instead of going through the schema engine: these tests cover the write path and
        /// creating the table is only a precondition. Going through <c>TableSchemaBuilder</c> would drag in definition
        /// loading and upgrade planning too.
        /// </remarks>
        private static string CreateTableSql(DatabaseType databaseType, string qt, string qRowId, string qName)
            => databaseType switch
            {
                DatabaseType.SQLServer =>
                    $"CREATE TABLE {qt} ({qRowId} NVARCHAR(50) NOT NULL PRIMARY KEY, {qName} NVARCHAR(50) NULL)",
                DatabaseType.MySQL =>
                    $"CREATE TABLE {qt} ({qRowId} VARCHAR(50) NOT NULL PRIMARY KEY, {qName} VARCHAR(50) NULL)",
                DatabaseType.Oracle =>
                    $"CREATE TABLE {qt} ({qRowId} VARCHAR2(50) NOT NULL PRIMARY KEY, {qName} VARCHAR2(50))",
                DatabaseType.PostgreSQL =>
                    $"CREATE TABLE {qt} ({qRowId} VARCHAR(50) NOT NULL PRIMARY KEY, {qName} VARCHAR(50) NULL)",
                DatabaseType.SQLite =>
                    $"CREATE TABLE {qt} ({qRowId} TEXT PRIMARY KEY, {qName} TEXT)",
                _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, "Database type not covered."),
            };
    }
}
