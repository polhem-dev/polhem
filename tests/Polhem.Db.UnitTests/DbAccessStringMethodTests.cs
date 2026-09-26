using System.ComponentModel;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    public class DbAccessStringMethodTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public DbAccessStringMethodTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteNonQuery string overload returns the affected row count")]
        public void ExecuteNonQuery_ValidSql_ReturnsRowsAffected()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            int affected = dbAccess.ExecuteNonQuery(
                "UPDATE st_user SET note={1} WHERE sys_id={0}", "001", "test-string-overload");
            Assert.True(affected >= 0);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteScalar string overload returns the scalar value")]
        public void ExecuteScalar_ValidSql_ReturnsScalarValue()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            object? value = dbAccess.ExecuteScalar(
                "SELECT COUNT(*) FROM st_user WHERE sys_id={0}", "001");
            Assert.NotNull(value);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteDataTable string overload returns a DataTable")]
        public void ExecuteDataTable_ValidSql_ReturnsDataTable()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var table = dbAccess.ExecuteDataTable(
                "SELECT sys_id FROM st_user WHERE sys_id={0}", "001");
            Assert.NotNull(table);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteNonQueryAsync string overload returns the affected row count")]
        public async Task ExecuteNonQueryAsync_ValidSql_ReturnsRowsAffected()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            int affected = await dbAccess.ExecuteNonQueryAsync(
                "UPDATE st_user SET note={1} WHERE sys_id={0}", "001", "test-async-overload");
            Assert.True(affected >= 0);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteScalarAsync string overload returns the scalar value")]
        public async Task ExecuteScalarAsync_ValidSql_ReturnsScalarValue()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            object? value = await dbAccess.ExecuteScalarAsync(
                "SELECT COUNT(*) FROM st_user WHERE sys_id={0}", "001");
            Assert.NotNull(value);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteDataTableAsync string overload returns a DataTable")]
        public async Task ExecuteDataTableAsync_ValidSql_ReturnsDataTable()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var table = await dbAccess.ExecuteDataTableAsync(
                "SELECT sys_id FROM st_user WHERE sys_id={0}", "001");
            Assert.NotNull(table);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatch runs the batch commands without a transaction")]
        public void ExecuteBatch_WithoutTransaction_Succeeds()
        {
            var batch = new DbBatchSpec { UseTransaction = false };
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_user WHERE sys_id={0}", "001"));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = dbAccess.ExecuteBatch(batch);

            Assert.NotNull(result);
            Assert.Single(result.Results);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatchAsync runs the batch commands asynchronously without a transaction")]
        public async Task ExecuteBatchAsync_WithoutTransaction_Succeeds()
        {
            var batch = new DbBatchSpec { UseTransaction = false };
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_user WHERE sys_id={0}", "001"));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = await dbAccess.ExecuteBatchAsync(batch);

            Assert.NotNull(result);
            Assert.Single(result.Results);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Execute overload with a DbTransaction runs the command")]
        public void Execute_WithTransaction_NonQuery_Succeeds()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            using var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver");
            conn.Open();
            using var tran = conn.BeginTransaction();

            var spec = new DbCommandSpec(DbCommandKind.NonQuery,
                "UPDATE st_user SET note={1} WHERE sys_id={0}", "001", "tx-overload");
            var result = dbAccess.Execute(spec, tran);
            tran.Rollback();

            Assert.NotNull(result);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteAsync overload with a DbTransaction runs the command asynchronously")]
        public async Task ExecuteAsync_WithTransaction_NonQuery_Succeeds()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            using var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver");
            await conn.OpenAsync();
            await using var tran = await conn.BeginTransactionAsync();

            var spec = new DbCommandSpec(DbCommandKind.NonQuery,
                "UPDATE st_user SET note={1} WHERE sys_id={0}", "001", "tx-async-overload");
            var result = await dbAccess.ExecuteAsync(spec, tran);
            await tran.RollbackAsync();

            Assert.NotNull(result);
        }
    }
}
