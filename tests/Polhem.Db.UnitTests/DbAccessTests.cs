using System.ComponentModel;
using System.Security.Cryptography;
using System.Globalization;
using Polhem.Db.Dml;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Db.Manager;

namespace Polhem.Db.UnitTests
{
    public class DbAccessTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DbAccessTests(SharedDbFixture fx) { _fx = fx; }
        public class User
        {
            public string? UserID { get; set; }
            public string? UserName { get; set; }
            public DateTime InsertTime { get; set; }
        }

        public class User2
        {
            public string? UserID { get; set; }
            public string? UserName { get; set; }
            public string? AccessToken { get; set; }
        }

        /// <summary>
        /// Runs SQL queries and gets a DataTable.
        /// </summary>
        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteDataTable returns a valid DataTable for several parameterized queries")]
        public void ExecuteDataTable_VariousParameterFormats_ReturnsDataTable()
        {
            // The connection is managed by `DbAccess`.
            string sql = "SELECT * FROM st_user";
            var command = new DbCommandSpec(DbCommandKind.DataTable, sql);
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = dbAccess.Execute(command);
            Assert.NotNull(result.Table);

            // The connection is managed by the caller.
            using (var conn = _fx.GetRequiredService<IDbConnectionManager>().CreateConnection("common_sqlserver"))
            {
                dbAccess = new DbAccess(conn, DatabaseType.SQLServer);
                result = dbAccess.Execute(command);
                Assert.NotNull(result.Table);
            }

            sql = "SELECT * FROM st_user WHERE sys_id = {0} OR sys_id = {1} ";
            command = new DbCommandSpec(DbCommandKind.DataTable, sql);
            command.Parameters.Add("p1", "001");
            command.Parameters.Add("p2", "002");
            dbAccess = _fx.NewDbAccess("common_sqlserver");
            result = dbAccess.Execute(command);
            Assert.NotNull(result.Table);

            command = new DbCommandSpec(DbCommandKind.DataTable, sql, "001", "002");
            dbAccess = _fx.NewDbAccess("common_sqlserver");
            result = dbAccess.Execute(command);
            Assert.NotNull(result.Table);

            var parameters = new Dictionary<string, object>
            {
                { "p1", "001" },
                { "p2", "002" }
            };
            sql = "SELECT * FROM st_user WHERE sys_id = {p1} OR sys_id = {p2} ";
            command = new DbCommandSpec(DbCommandKind.DataTable, sql, parameters);
            result = dbAccess.Execute(command);
            Assert.NotNull(result.Table);
        }

        /// <summary>
        /// Runs a SQL query asynchronously and gets a DataTable.
        /// </summary>
        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteDataTableAsync returns a DataTable with rows")]
        public async Task ExecuteDataTableAsync_ValidQuery_ReturnsNonEmptyDataTable()
        {
            string sql = "SELECT * FROM st_user";
            var command = new DbCommandSpec(DbCommandKind.DataTable, sql);
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var reulst = await dbAccess.ExecuteAsync(command);
            var table = reulst.Table;
            Assert.NotNull(table);
            Assert.True(table.Rows.Count > 0);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteNonQuery updates data")]
        public void ExecuteNonQuery_UpdateRow_Executes()
        {
            int i = RandomNumberGenerator.GetInt32(0, 100);
            string sql = "Update st_user Set note={1} Where sys_id = {0}";
            var command = new DbCommandSpec(DbCommandKind.NonQuery, sql, "001", i);
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = dbAccess.Execute(command);
            Assert.NotNull(result);
            Assert.True(result.RowsAffected >= 0);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteNonQueryAsync updates data asynchronously")]
        public async Task ExecuteNonQueryAsync_UpdateRow_Executes()
        {
            int i = RandomNumberGenerator.GetInt32(0, 100);
            string sql = "Update st_user Set note={1} Where sys_id = {0}";
            var command = new DbCommandSpec(DbCommandKind.NonQuery, sql, "001", i);
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = await dbAccess.ExecuteAsync(command);
            Assert.NotNull(result);
            Assert.True(result.RowsAffected >= 0);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteScalar returns a single value")]
        public void ExecuteScalar_SelectSingleValue_ReturnsScalar()
        {
            string sql = "Select note From st_user Where sys_id = {0}";
            var command = new DbCommandSpec(DbCommandKind.Scalar, sql, "001");
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = dbAccess.Execute(command);
            Assert.NotNull(result);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Query returns a list of strongly typed objects")]
        public void Query_ValidSql_ReturnsMappedObjects()
        {
            string sql = "SELECT sys_id AS userID, sys_name AS UserName, sys_insert_time AS InsertTime FROM st_user";
            var command = new DbCommandSpec(DbCommandKind.DataTable, sql);
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var list = dbAccess.Query<User>(command);
            var list3 = dbAccess.Query<User2>(command);
            Assert.NotNull(list);
            Assert.NotNull(list3);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("QueryAsync returns a list of strongly typed objects")]
        public async Task QueryAsync_ValidSql_ReturnsMappedObjects()
        {
            string sql = "SELECT sys_id AS userID, sys_name AS UserName, sys_insert_time AS InsertTime FROM st_user";
            var command = new DbCommandSpec(DbCommandKind.DataTable, sql);
            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var list = await dbAccess.QueryAsync<User>(command);
            var list2 = await dbAccess.QueryAsync<User2>(command);
            Assert.NotNull(list);
            Assert.NotNull(list2);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("UpdateDataTable affects at least one row after a row is modified")]
        public void UpdateDataTable_ModifiedRow_AffectsRows()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");

            string sql = "SELECT * FROM st_user";
            var command = new DbCommandSpec(DbCommandKind.DataTable, sql);
            var result = dbAccess.Execute(command);
            var table = result.Table;
            Assert.NotNull(table);
            Assert.True(table.Rows.Count > 0, "st_user has no rows");

            int i = RandomNumberGenerator.GetInt32(0, 100);
            var row = table.Rows[0];
            row["note"] = i.ToString(CultureInfo.InvariantCulture);

            var tableSchema = _fx.GetRequiredService<IDefineAccess>().GetTableSchema("common", "st_user");
            var builder = new TableSchemaCommandBuilder(dbAccess.DatabaseType, tableSchema);
            var updateSpec = builder.BuildUpdateSpec(table);

            int affected = dbAccess.UpdateDataTable(updateSpec);

            Assert.True(affected > 0, "No row was updated");
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatch runs several commands in a transaction")]
        public void ExecuteBatch_WithTransaction_Succeeds()
        {
            var batch = new DbBatchSpec();
            batch.UseTransaction = true;
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.Scalar,
                    "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", "001"));
            int i = RandomNumberGenerator.GetInt32(0, 100);
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.NonQuery,
                     "UPDATE st_user SET note={1} WHERE sys_id = {0}", "001", i));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = dbAccess.ExecuteBatch(batch);
            Assert.NotNull(result);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatchAsync runs several commands in a transaction asynchronously")]
        public async Task ExecuteBatchAsync_WithTransaction_Succeeds()
        {
            var batch = new DbBatchSpec();
            batch.UseTransaction = true;
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.Scalar,
                    "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", "001"));
            int i = RandomNumberGenerator.GetInt32(0, 100);
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.NonQuery,
                     "UPDATE st_user SET note={1} WHERE sys_id = {0}", "001", i));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = await dbAccess.ExecuteBatchAsync(batch);
            Assert.NotNull(result);
        }
    }
}
