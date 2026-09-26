using System.ComponentModel;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    public class DbAccessTransactionTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public DbAccessTransactionTests(SharedDbFixture fx) { _fx = fx; }

        /// <summary>
        /// Fake transaction whose connection is always null, for testing null-connection guards.
        /// </summary>
        private sealed class NullConnectionTransaction : DbTransaction
        {
            protected override DbConnection? DbConnection => null;
            public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
            public override void Commit() { }
            public override void Rollback() { }
        }

        // ── null-connection guard (no DB required) ──────────────────────────

        [Fact]
        [DisplayName("Execute(spec, transaction) throws InvalidOperationException when the transaction's connection is null")]
        public void Execute_WithTransaction_NullConnection_ThrowsInvalidOperation()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1");
            using var tran = new NullConnectionTransaction();

            Assert.Throws<InvalidOperationException>(() => dbAccess.Execute(spec, tran));
        }

        [Fact]
        [DisplayName("ExecuteAsync(spec, transaction) throws InvalidOperationException when the transaction's connection is null")]
        public async Task ExecuteAsync_WithTransaction_NullConnection_ThrowsInvalidOperation()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1");
            using var tran = new NullConnectionTransaction();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbAccess.ExecuteAsync(spec, tran));
        }

        // ── ExecuteAsync(spec) - Scalar branch (no transaction overload) ────

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteAsync(DbCommandSpec) of kind Scalar returns the scalar value")]
        public async Task ExecuteAsync_ScalarKind_ReturnsScalar()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", "001");

            var result = await dbAccess.ExecuteAsync(spec);

            Assert.NotNull(result);
            Assert.NotNull(result.Scalar);
        }

        // ── Execute(spec, transaction) - Scalar + DataTable branches ────────

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Execute(spec, transaction) of kind Scalar returns the scalar value")]
        public void Execute_WithTransaction_ScalarKind_ReturnsScalar()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            using var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver");
            conn.Open();
            using var tran = conn.BeginTransaction();

            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", "001");
            var result = dbAccess.Execute(spec, tran);
            tran.Rollback();

            Assert.NotNull(result);
            Assert.NotNull(result.Scalar);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Execute(spec, transaction) of kind DataTable returns the table")]
        public void Execute_WithTransaction_DataTableKind_ReturnsTable()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            using var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver");
            conn.Open();
            using var tran = conn.BeginTransaction();

            var spec = new DbCommandSpec(DbCommandKind.DataTable,
                "SELECT sys_id FROM st_user WHERE sys_id = {0}", "001");
            var result = dbAccess.Execute(spec, tran);
            tran.Rollback();

            Assert.NotNull(result);
            Assert.NotNull(result.Table);
        }

        // ── ExecuteAsync(spec, transaction) - Scalar + DataTable branches ───

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteAsync(spec, transaction) of kind Scalar returns the scalar value")]
        public async Task ExecuteAsync_WithTransaction_ScalarKind_ReturnsScalar()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            using var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver");
            await conn.OpenAsync();
            await using var tran = await conn.BeginTransactionAsync();

            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", "001");
            var result = await dbAccess.ExecuteAsync(spec, tran);
            await tran.RollbackAsync();

            Assert.NotNull(result);
            Assert.NotNull(result.Scalar);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteAsync(spec, transaction) of kind DataTable returns the table")]
        public async Task ExecuteAsync_WithTransaction_DataTableKind_ReturnsTable()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            using var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver");
            await conn.OpenAsync();
            await using var tran = await conn.BeginTransactionAsync();

            var spec = new DbCommandSpec(DbCommandKind.DataTable,
                "SELECT sys_id FROM st_user WHERE sys_id = {0}", "001");
            var result = await dbAccess.ExecuteAsync(spec, tran);
            await tran.RollbackAsync();

            Assert.NotNull(result);
            Assert.NotNull(result.Table);
        }
    }
}
