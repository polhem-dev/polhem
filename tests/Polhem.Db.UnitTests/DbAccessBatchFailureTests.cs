using System.ComponentModel;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// A failing batch is rolled back and its exception rethrown unchanged, with the failing command's index in
    /// <see cref="Exception.Data"/>, so a caller still tells a database error and a cancellation apart.
    /// </summary>
    public class DbAccessBatchFailureTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DbAccessBatchFailureTests(SharedDbFixture fx) { _fx = fx; }

        private DbAccess NewDbAccess(DatabaseType databaseType)
            => new(TestDbConventions.GetDatabaseId(databaseType), _fx.GetRequiredService<IDbConnectionManager>());

        // A table of its own per run, so a rollback can be checked by counting rows from another connection.
        private static string NewTableName() => "tb_" + Guid.NewGuid().ToString("N")[..12];

        private static DbBatchSpec InsertThenDuplicate(string table) => new()
        {
            UseTransaction = true,
            Commands =
            [
                new DbCommandSpec(DbCommandKind.NonQuery, $"INSERT INTO {table} (id) VALUES (1)"),
                new DbCommandSpec(DbCommandKind.NonQuery, $"INSERT INTO {table} (id) VALUES (1)"),
            ],
        };

        private static long CountRows(DbAccess dbAccess, string table)
            => Convert.ToInt64(dbAccess.ExecuteScalar($"SELECT COUNT(*) FROM {table}"), CultureInfo.InvariantCulture);

        private void RunDuplicateKey(DatabaseType databaseType, bool async)
        {
            var dbAccess = NewDbAccess(databaseType);
            var table = NewTableName();
            dbAccess.ExecuteNonQuery($"CREATE TABLE {table} (id INT NOT NULL PRIMARY KEY)");
            try
            {
                var batch = InsertThenDuplicate(table);
                var ex = async
                    ? Assert.ThrowsAny<DbException>(() => dbAccess.ExecuteBatchAsync(batch).GetAwaiter().GetResult())
                    : Assert.ThrowsAny<DbException>(() => dbAccess.ExecuteBatch(batch));

                Assert.Equal(1, ex.Data[DbBatchSpec.FailedIndexDataKey]);
                Assert.Equal(0L, CountRows(dbAccess, table));
            }
            finally
            {
                dbAccess.ExecuteNonQuery($"DROP TABLE {table}");
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: ExecuteBatch rethrows the database error with the failing index and rolls the batch back")]
        public void ExecuteBatch_Sqlite_DuplicateKey_RethrowsDbExceptionAndRollsBack()
            => RunDuplicateKey(DatabaseType.SQLite, async: false);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: ExecuteBatchAsync rethrows the database error with the failing index and rolls the batch back")]
        public void ExecuteBatchAsync_Sqlite_DuplicateKey_RethrowsDbExceptionAndRollsBack()
            => RunDuplicateKey(DatabaseType.SQLite, async: true);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: ExecuteBatch rethrows the database error with the failing index and rolls the batch back")]
        public void ExecuteBatch_SqlServer_DuplicateKey_RethrowsDbExceptionAndRollsBack()
            => RunDuplicateKey(DatabaseType.SQLServer, async: false);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: ExecuteBatchAsync rethrows the database error with the failing index and rolls the batch back")]
        public void ExecuteBatchAsync_SqlServer_DuplicateKey_RethrowsDbExceptionAndRollsBack()
            => RunDuplicateKey(DatabaseType.SQLServer, async: true);

        [Fact]
        [DisplayName("ExecuteBatchAsync cancelled inside the batch throws OperationCanceledException with the command's index")]
        public async Task ExecuteBatchAsync_CancelledInsideBatch_ThrowsOperationCanceledWithIndex()
        {
            // An open external connection and no transaction let the batch reach its command loop before anything else
            // sees the cancelled token.
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbAccess = new DbAccess(connection, DatabaseType.SQLite);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var batch = new DbBatchSpec { Commands = [new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1")] };

            var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dbAccess.ExecuteBatchAsync(batch, cts.Token));

            Assert.Equal(0, ex.Data[DbBatchSpec.FailedIndexDataKey]);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: ExecuteBatchAsync cancelled while a command runs throws OperationCanceledException")]
        public async Task ExecuteBatchAsync_SqlServer_CancelledWhileRunning_ThrowsOperationCanceled()
        {
            // SqlClient reports a command cancelled mid-flight as a `SqlException`; the batch turns it into a cancellation.
            var dbAccess = NewDbAccess(DatabaseType.SQLServer);
            // A pooled connection makes opening and beginning quick, so the cancellation lands while the command waits.
            dbAccess.ExecuteScalar("SELECT 1");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var batch = new DbBatchSpec
            {
                UseTransaction = true,
                Commands = [new DbCommandSpec(DbCommandKind.NonQuery, "WAITFOR DELAY '00:00:30'")],
            };

            var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dbAccess.ExecuteBatchAsync(batch, cts.Token));

            Assert.Equal(0, ex.Data[DbBatchSpec.FailedIndexDataKey]);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ExecuteBatch rethrows NotSupportedException for an unknown command kind, with its index")]
        public void ExecuteBatch_UnknownKind_RethrowsNotSupportedWithIndex()
        {
            var dbAccess = NewDbAccess(DatabaseType.SQLite);
            var batch = new DbBatchSpec
            {
                Commands =
                [
                    new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1"),
                    new DbCommandSpec((DbCommandKind)99, "SELECT 1"),
                ],
            };

            var ex = Assert.Throws<NotSupportedException>(() => dbAccess.ExecuteBatch(batch));

            Assert.Equal(1, ex.Data[DbBatchSpec.FailedIndexDataKey]);
        }
    }
}
