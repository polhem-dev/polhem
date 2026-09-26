using System.ComponentModel;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Logging;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Verifies the anomaly detection of <c>DbAccess</c> (<c>DbAccess.Anomaly.cs</c>): the threshold checks, the Kind
    /// classification, and the constraint that writing anomalies is best-effort and must not change the result of a
    /// command.
    /// </summary>
    /// <remarks>
    /// Real commands run on SQLite instead of a mocked <c>DbAccess</c>: the threshold checks consume the actual
    /// RowsAffected / Rows.Count / exception type, and a fake execution result would bypass the logic under test.
    /// </remarks>
    public class DbAccessAnomalyTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DbAccessAnomalyTests(SharedDbFixture fx) { _fx = fx; }

        /// <summary>
        /// A test writer that collects <see cref="DbAnomalyEntry"/> records.
        /// </summary>
        private sealed class CapturingAnomalyLogWriter : IAnomalyLogWriter
        {
            public List<DbAnomalyEntry> Entries { get; } = [];

            public void Write(AnomalyEntry entry)
            {
                if (entry is DbAnomalyEntry anomaly) { Entries.Add(anomaly); }
            }
        }

        private DbAccess NewSqliteDbAccess(
            IAnomalyLogWriter? writer, DbAccessAnomalyLogOptions? options, int maxCommandTimeout = 0)
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            return new DbAccess(databaseId, _fx.GetRequiredService<IDbConnectionManager>(),
                maxCommandTimeout, writer, options);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Without a writer the command runs as usual and no anomaly is written")]
        public void NoWriter_ExecutesWithoutAnomaly()
        {
            var writer = new CapturingAnomalyLogWriter();
            var dbAccess = NewSqliteDbAccess(writer: null, options: null);

            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1"));

            Assert.Equal(1L, Convert.ToInt64(result.Scalar, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Empty(writer.Entries);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Level None writes no anomaly even with every threshold enabled")]
        public void LevelNone_WritesNothing()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions
            {
                Level = DbAccessAnomalyLogLevel.None,
                ExecutionTimeThreshold = 1,
                ResultRowThreshold = 1
            };
            var dbAccess = NewSqliteDbAccess(writer, options);

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, "SELECT 1 UNION ALL SELECT 2"));

            Assert.Empty(writer.Entries);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Level Error does not record Slow or LargeResult for a successful command")]
        public void LevelError_SkipsSuccessAnomalies()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions
            {
                Level = DbAccessAnomalyLogLevel.Error,
                ResultRowThreshold = 1
            };
            var dbAccess = NewSqliteDbAccess(writer, options);

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, "SELECT 1 UNION ALL SELECT 2"));

            Assert.Empty(writer.Entries);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A result row count over the threshold records LargeResult with the actual row count")]
        public void ResultRowThresholdExceeded_LogsLargeResult()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions
            {
                Level = DbAccessAnomalyLogLevel.Warning,
                ResultRowThreshold = 1,
                AffectedRowThreshold = 0,
                ExecutionTimeThreshold = 0
            };
            var dbAccess = NewSqliteDbAccess(writer, options);

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, "SELECT 1 UNION ALL SELECT 2"));

            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AnomalyKind.LargeResult, entry.Kind);
            Assert.Equal(2, entry.ResultRows);
            Assert.Equal(TestDbConventions.GetDatabaseId(DatabaseType.SQLite), entry.DatabaseId);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("An affected row count over the threshold records LargeAffected")]
        public void AffectedRowThresholdExceeded_LogsLargeAffected()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions
            {
                Level = DbAccessAnomalyLogLevel.Warning,
                AffectedRowThreshold = 1,
                ResultRowThreshold = 0,
                ExecutionTimeThreshold = 0
            };
            var dbAccess = NewSqliteDbAccess(writer, options);
            var table = $"t_anomaly_affected_{Guid.NewGuid():N}";

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                $"CREATE TABLE {table} (id INTEGER)"));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                $"INSERT INTO {table} (id) SELECT 1 UNION ALL SELECT 2"));

            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AnomalyKind.LargeAffected, entry.Kind);
            Assert.Equal(2, entry.AffectedRows);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A duration threshold of 0 records no Slow")]
        public void ExecutionTimeThresholdDisabled_SkipsSlow()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions
            {
                Level = DbAccessAnomalyLogLevel.Warning,
                ExecutionTimeThreshold = 0,
                AffectedRowThreshold = 0,
                ResultRowThreshold = 0
            };
            var dbAccess = NewSqliteDbAccess(writer, options);

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1"));

            Assert.Empty(writer.Entries);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A failed command records Error, keeps the original exception and logs no parameter values in the command text")]
        public void CommandFails_LogsErrorAndRethrows()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions { Level = DbAccessAnomalyLogLevel.Error };
            var dbAccess = NewSqliteDbAccess(writer, options);
            var commandText = "SELECT * FROM __no_such_table__ WHERE id = {0}";

            var exception = Record.Exception(() =>
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, commandText, 123)));

            Assert.NotNull(exception);
            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AnomalyKind.Error, entry.Kind);
            Assert.Equal(commandText, entry.Command);
            Assert.DoesNotContain("123", entry.Command, StringComparison.Ordinal);
            Assert.False(string.IsNullOrEmpty(entry.ErrorType));
            Assert.False(string.IsNullOrEmpty(entry.ErrorMessage));
            // The message is flattened to a single line before it is stored, so a log field never carries a line break.
            Assert.DoesNotContain('\n', entry.ErrorMessage!);
            Assert.DoesNotContain('\r', entry.ErrorMessage!);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("An exception message containing timeout is classified as Timeout")]
        public void TimeoutWorded_ClassifiesAsTimeout()
        {
            var writer = new CapturingAnomalyLogWriter();
            var options = new DbAccessAnomalyLogOptions { Level = DbAccessAnomalyLogLevel.Error };
            var dbAccess = NewSqliteDbAccess(writer, options);

            // SQLite's error message for an unknown function includes the function name, which makes the message
            // match in `IsTimeout` without really letting the command time out (a timeout test is a sure source of
            // flakiness on CI).
            var exception = Record.Exception(() =>
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, "SELECT timeout(1)")));

            Assert.NotNull(exception);
            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AnomalyKind.Timeout, entry.Kind);
        }
    }
}
