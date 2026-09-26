using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using Polhem.Db.Dml;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;

namespace Polhem.Db.UnitTests
{
    public class DbAccessIsolationLevelTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DbAccessIsolationLevelTests(SharedDbFixture fx) { _fx = fx; }
        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatch runs a batch transaction with the given IsolationLevel")]
        public void ExecuteBatch_WithIsolationLevel_Succeeds()
        {
            var batch = new DbBatchSpec
            {
                UseTransaction = true,
                IsolationLevel = IsolationLevel.ReadCommitted
            };
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", "001"));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = dbAccess.ExecuteBatch(batch);

            Assert.NotNull(result);
            Assert.Single(result.Results);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatchAsync runs a batch transaction asynchronously with the given IsolationLevel")]
        public async Task ExecuteBatchAsync_WithIsolationLevel_Succeeds()
        {
            var batch = new DbBatchSpec
            {
                UseTransaction = true,
                IsolationLevel = IsolationLevel.ReadCommitted
            };
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM st_user WHERE sys_id = {0}", "001"));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = await dbAccess.ExecuteBatchAsync(batch);

            Assert.NotNull(result);
            Assert.Single(result.Results);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatch returns the table of a DataTable command in the batch")]
        public void ExecuteBatch_DataTableCommand_Succeeds()
        {
            var batch = new DbBatchSpec { UseTransaction = false };
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.DataTable,
                "SELECT sys_id FROM st_user WHERE sys_id = {0}", "001"));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = dbAccess.ExecuteBatch(batch);

            Assert.NotNull(result);
            Assert.Single(result.Results);
            Assert.NotNull(result.Results[0].Table);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("ExecuteBatchAsync returns the table of a DataTable command in the batch")]
        public async Task ExecuteBatchAsync_DataTableCommand_Succeeds()
        {
            var batch = new DbBatchSpec { UseTransaction = false };
            batch.Commands.Add(new DbCommandSpec(DbCommandKind.DataTable,
                "SELECT sys_id FROM st_user WHERE sys_id = {0}", "001"));

            var dbAccess = _fx.NewDbAccess("common_sqlserver");
            var result = await dbAccess.ExecuteBatchAsync(batch);

            Assert.NotNull(result);
            Assert.Single(result.Results);
            Assert.NotNull(result.Results[0].Table);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("UpdateDataTable updates with the given IsolationLevel")]
        public void UpdateDataTable_WithIsolationLevel_Succeeds()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlserver");

            var cmd = new DbCommandSpec(DbCommandKind.DataTable,
                "SELECT * FROM st_user WHERE sys_id = {0}", "001");
            var table = dbAccess.Execute(cmd).Table;
            Assert.NotNull(table);
            Assert.True(table.Rows.Count > 0, "st_user has no row with sys_id='001'");

            int rnd = RandomNumberGenerator.GetInt32(0, 100);
            table.Rows[0]["note"] = rnd.ToString(CultureInfo.InvariantCulture);

            var tableSchema = _fx.GetRequiredService<IDefineAccess>().GetTableSchema("common", "st_user");
            var builder = new TableSchemaCommandBuilder(dbAccess.DatabaseType, tableSchema);
            var updateSpec = builder.BuildUpdateSpec(table);
            updateSpec.UseTransaction = true;
            updateSpec.IsolationLevel = IsolationLevel.ReadCommitted;

            int affected = dbAccess.UpdateDataTable(updateSpec);
            Assert.True(affected > 0);
        }
    }
}
