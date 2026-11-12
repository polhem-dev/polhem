using System.ComponentModel;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Every way <see cref="DbAccess"/> runs SQL records its anomalies, not only <see cref="DbAccess.Execute(DbCommandSpec)"/>:
    /// the asynchronous execute, the batches, the typed queries and the <see cref="DataTable"/> write.
    /// </summary>
    /// <remarks>
    /// The thresholds and the Kind classification are covered by <see cref="DbAccessAnomalyTests"/>; these tests only
    /// check that each entry point reaches the detection.
    /// </remarks>
    public class DbAccessAnomalyCoverageTests : IClassFixture<SharedDbFixture>
    {
        private const string MissingTableQuery = "SELECT id FROM no_such_table_for_anomaly";

        private readonly SharedDbFixture _fx;

        public DbAccessAnomalyCoverageTests(SharedDbFixture fx) { _fx = fx; }

        private sealed class CapturingAnomalyLogWriter : IAnomalyLogWriter
        {
            public List<DbAnomalyEntry> Entries { get; } = [];

            public void Write(AnomalyEntry entry)
            {
                if (entry is DbAnomalyEntry anomaly) { Entries.Add(anomaly); }
            }
        }

        private sealed class ThrowingAnomalyLogWriter : IAnomalyLogWriter
        {
            public void Write(AnomalyEntry entry) => throw new InvalidOperationException("The anomaly writer is down.");
        }

        private sealed class Row
        {
            public long Id { get; set; }
        }

        private (DbAccess DbAccess, CapturingAnomalyLogWriter Writer) NewDbAccess(DbAccessAnomalyLogLevel level, int resultRowThreshold = 0)
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions { Level = level, ResultRowThreshold = resultRowThreshold };
            var dbAccess = new DbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLite),
                _fx.GetRequiredService<IDbConnectionManager>(), 0, writer, options);
            return (dbAccess, writer);
        }

        private static void AssertSingleError(CapturingAnomalyLogWriter writer, string commandText)
        {
            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AnomalyKind.Error, entry.Kind);
            Assert.Equal(commandText, entry.Command);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing ExecuteAsync records an Error anomaly")]
        public async Task ExecuteAsync_Failure_RecordsError()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);

            await Assert.ThrowsAnyAsync<DbException>(
                () => dbAccess.ExecuteAsync(new DbCommandSpec(DbCommandKind.DataTable, MissingTableQuery)));

            AssertSingleError(writer, MissingTableQuery);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing command in ExecuteBatch records an Error anomaly against that command")]
        public void ExecuteBatch_Failure_RecordsErrorForFailingCommand()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);
            var batch = new DbBatchSpec
            {
                Commands = [new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1"), new DbCommandSpec(DbCommandKind.DataTable, MissingTableQuery)],
            };

            Assert.ThrowsAny<DbException>(() => dbAccess.ExecuteBatch(batch));

            AssertSingleError(writer, MissingTableQuery);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing command in ExecuteBatchAsync records an Error anomaly against that command")]
        public async Task ExecuteBatchAsync_Failure_RecordsErrorForFailingCommand()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);
            var batch = new DbBatchSpec
            {
                Commands = [new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1"), new DbCommandSpec(DbCommandKind.DataTable, MissingTableQuery)],
            };

            await Assert.ThrowsAnyAsync<DbException>(() => dbAccess.ExecuteBatchAsync(batch));

            AssertSingleError(writer, MissingTableQuery);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A command in ExecuteBatch over the result row threshold records LargeResult")]
        public void ExecuteBatch_LargeResult_RecordsLargeResult()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Warning, resultRowThreshold: 1);
            var batch = new DbBatchSpec { Commands = [new DbCommandSpec(DbCommandKind.DataTable, "SELECT 1 UNION ALL SELECT 2")] };

            dbAccess.ExecuteBatch(batch);

            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AnomalyKind.LargeResult, entry.Kind);
            Assert.Equal(2, entry.ResultRows);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing Query records an Error anomaly")]
        public void Query_Failure_RecordsError()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);

            Assert.ThrowsAny<DbException>(() => dbAccess.Query<Row>(new DbCommandSpec(DbCommandKind.DataTable, MissingTableQuery)));

            AssertSingleError(writer, MissingTableQuery);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing QueryAsync records an Error anomaly")]
        public async Task QueryAsync_Failure_RecordsError()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);

            await Assert.ThrowsAnyAsync<DbException>(
                () => dbAccess.QueryAsync<Row>(new DbCommandSpec(DbCommandKind.DataTable, MissingTableQuery)));

            AssertSingleError(writer, MissingTableQuery);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A Query over the result row threshold records LargeResult with the mapped row count")]
        public void Query_LargeResult_RecordsLargeResult()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Warning, resultRowThreshold: 1);

            var rows = dbAccess.Query<Row>(new DbCommandSpec(DbCommandKind.DataTable, "SELECT 1 AS id UNION ALL SELECT 2"));

            Assert.Equal(2, rows.Count);
            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AnomalyKind.LargeResult, entry.Kind);
            Assert.Equal(2, entry.ResultRows);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing UpdateDataTable records an Error anomaly naming its commands")]
        public void UpdateDataTable_Failure_RecordsError()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);
            const string insert = "INSERT INTO no_such_table_for_anomaly (id) VALUES (1)";
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Rows.Add(1);
            var spec = new DataTableUpdateSpec
            {
                DataTable = table,
                InsertCommand = new DbCommandSpec(DbCommandKind.NonQuery, insert),
            };

            Assert.ThrowsAny<DbException>(() => dbAccess.UpdateDataTable(spec));

            AssertSingleError(writer, insert);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing ExecuteAsync on a caller's transaction records an Error anomaly")]
        public async Task ExecuteAsyncWithTransaction_Failure_RecordsError()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await Assert.ThrowsAnyAsync<DbException>(
                () => dbAccess.ExecuteAsync(new DbCommandSpec(DbCommandKind.DataTable, MissingTableQuery), transaction));

            AssertSingleError(writer, MissingTableQuery);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failing UpdateDataTables records an Error anomaly naming the failing spec's commands")]
        public void UpdateDataTables_Failure_RecordsError()
        {
            var (dbAccess, writer) = NewDbAccess(DbAccessAnomalyLogLevel.Error);
            const string insert = "INSERT INTO no_such_table_for_anomaly (id) VALUES (1)";
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Rows.Add(1);
            var spec = new DataTableUpdateSpec { DataTable = table, InsertCommand = new DbCommandSpec(DbCommandKind.NonQuery, insert) };

            Assert.ThrowsAny<DbException>(() => dbAccess.UpdateDataTables([spec]));

            AssertSingleError(writer, insert);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A writer that throws does not fail a batch that succeeded")]
        public void ThrowingWriter_SuccessfulBatch_StillSucceeds()
        {
            var options = new DbAccessAnomalyLogOptions { Level = DbAccessAnomalyLogLevel.Warning, ResultRowThreshold = 1 };
            var dbAccess = new DbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLite),
                _fx.GetRequiredService<IDbConnectionManager>(), 0, new ThrowingAnomalyLogWriter(), options);
            var batch = new DbBatchSpec
            {
                UseTransaction = true,
                Commands = [new DbCommandSpec(DbCommandKind.DataTable, "SELECT 1 UNION ALL SELECT 2")],
            };

            var result = dbAccess.ExecuteBatch(batch);

            Assert.Equal(2, Assert.Single(result.Results).Table!.Rows.Count);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A writer that throws leaves a failing command's own exception in place")]
        public void ThrowingWriter_FailingCommand_KeepsDatabaseException()
        {
            var options = new DbAccessAnomalyLogOptions { Level = DbAccessAnomalyLogLevel.Error };
            var dbAccess = new DbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLite),
                _fx.GetRequiredService<IDbConnectionManager>(), 0, new ThrowingAnomalyLogWriter(), options);

            Assert.ThrowsAny<DbException>(() => dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, MissingTableQuery)));
        }
    }
}
