using System.ComponentModel;
using Polhem.Definition.Database;
using Microsoft.Data.SqlClient;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class DbAccessExtraTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public DbAccessExtraTests(SharedDbFixture fx) { _fx = fx; }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("DbAccess(string) throws ArgumentException for a blank databaseId")]
        public void Constructor_EmptyDatabaseId_Throws(string databaseId)
        {
            Assert.Throws<ArgumentException>(() => _fx.NewDbAccess(databaseId));
        }

        [Fact]
        [DisplayName("DbAccess(string) throws ArgumentNullException for a null databaseId")]
        public void Constructor_NullDatabaseId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _fx.NewDbAccess((string)null!));
        }

        [Fact]
        [DisplayName("DbAccess(DbConnection) throws ArgumentNullException for a null connection")]
        public void Constructor_NullExternalConnection_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DbAccess((System.Data.Common.DbConnection)null!, DatabaseType.SQLServer));
        }

        [Fact]
        [DisplayName("Execute(null) throws ArgumentNullException")]
        public void Execute_NullCommand_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            Assert.Throws<ArgumentNullException>(() => dbAccess.Execute(null!));
        }

        [Fact]
        [DisplayName("Execute(spec, null transaction) throws ArgumentNullException")]
        public void Execute_NullTransaction_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1");

            Assert.Throws<ArgumentNullException>(() => dbAccess.Execute(spec, null!));
        }

        [Fact]
        [DisplayName("ExecuteAsync(null) throws ArgumentNullException")]
        public async Task ExecuteAsync_NullCommand_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await dbAccess.ExecuteAsync(null!));
        }

        [Fact]
        [DisplayName("ExecuteAsync(spec, null transaction) throws ArgumentNullException")]
        public async Task ExecuteAsync_NullTransaction_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT 1");

            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await dbAccess.ExecuteAsync(spec, null!));
        }

        [Fact]
        [DisplayName("ExecuteBatch(null) throws ArgumentNullException")]
        public void ExecuteBatch_NullBatch_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            Assert.Throws<ArgumentNullException>(() => dbAccess.ExecuteBatch(null!));
        }

        [Fact]
        [DisplayName("ExecuteBatch throws ArgumentException for empty Commands")]
        public void ExecuteBatch_EmptyCommands_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var batch = new DbBatchSpec();

            Assert.Throws<ArgumentException>(() => dbAccess.ExecuteBatch(batch));
        }

        [Fact]
        [DisplayName("ExecuteBatchAsync(null) throws ArgumentNullException")]
        public async Task ExecuteBatchAsync_NullBatch_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await dbAccess.ExecuteBatchAsync(null!));
        }

        [Fact]
        [DisplayName("ExecuteBatchAsync throws ArgumentException for empty Commands")]
        public async Task ExecuteBatchAsync_EmptyCommands_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var batch = new DbBatchSpec();

            await Assert.ThrowsAsync<ArgumentException>(
                async () => await dbAccess.ExecuteBatchAsync(batch));
        }

        [Fact]
        [DisplayName("UpdateDataTable(null) throws ArgumentNullException")]
        public void UpdateDataTable_NullSpec_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            Assert.Throws<ArgumentNullException>(() => dbAccess.UpdateDataTable(null!));
        }

        [Fact]
        [DisplayName("UpdateDataTable throws ArgumentException when every command of the spec is null")]
        public void UpdateDataTable_AllNullCommands_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var spec = new DataTableUpdateSpec
            {
                DataTable = new System.Data.DataTable(),
                InsertCommand = null,
                UpdateCommand = null,
                DeleteCommand = null
            };

            Assert.Throws<ArgumentException>(() => dbAccess.UpdateDataTable(spec));
        }

        [Fact]
        [DisplayName("Query(null) throws ArgumentNullException")]
        public void Query_NullCommand_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            Assert.Throws<ArgumentNullException>(() => dbAccess.Query<object>(null!));
        }

        [Fact]
        [DisplayName("QueryAsync(null) throws ArgumentNullException")]
        public async Task QueryAsync_NullCommand_Throws()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await dbAccess.QueryAsync<object>(null!));
        }

        [Fact]
        [DisplayName("ToString includes the DatabaseType and the provider name")]
        public void ToString_ContainsTypeAndProvider()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);

            var text = dbAccess.ToString();

            Assert.Contains("DbAccess", text);
            Assert.Contains("DatabaseType", text);
            Assert.Contains("Provider", text);
        }

        [Fact]
        [DisplayName("UpdateDataTable throws ArgumentException for a null DataTable")]
        public void UpdateDataTable_NullDataTable_ThrowsArgumentException()
        {
            using var conn = new SqlConnection();
            var dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
            var spec = new DataTableUpdateSpec
            {
                DataTable = null!,
                InsertCommand = new DbCommandSpec(DbCommandKind.NonQuery, "INSERT INTO t VALUES ({0})", "x")
            };

            Assert.Throws<ArgumentException>(() => dbAccess.UpdateDataTable(spec));
        }
    }
}
