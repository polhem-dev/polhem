using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class TableUpgradeOrchestratorIntegrationTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public TableUpgradeOrchestratorIntegrationTests(SharedDbFixture fx) { _fx = fx; }

        private const string DatabaseId = "common_sqlserver";

        private TableUpgradeOrchestrator CreateOrchestrator()
            => new(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());

        private static TableSchema BuildSchema(string tableName, int nameLength = 50)
        {
            var schema = new TableSchema { TableName = tableName };
            schema.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, nameLength);
            schema.Indexes!.AddPrimaryKey("sys_rowid");
            return schema;
        }

        private void DropIfExists(string tableName)
        {
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            var sql = $"IF OBJECT_ID(N'{tableName}', N'U') IS NOT NULL DROP TABLE [{tableName}];";
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql));
        }

        private bool TableExists(string tableName)
        {
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM sys.tables WHERE name = {0}", tableName);
            var result = dbAccess.Execute(spec);
            return Convert.ToInt32(result.Scalar!, System.Globalization.CultureInfo.InvariantCulture) > 0;
        }

        private bool ColumnExists(string tableName, string columnName)
        {
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID({0}) AND name = {1}",
                tableName, columnName);
            var result = dbAccess.Execute(spec);
            return Convert.ToInt32(result.Scalar!, System.Globalization.CultureInfo.InvariantCulture) > 0;
        }

        private int GetColumnLength(string tableName, string columnName)
        {
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT max_length FROM sys.columns WHERE object_id = OBJECT_ID({0}) AND name = {1}",
                tableName, columnName);
            var result = dbAccess.Execute(spec);
            // nvarchar max_length is bytes (2 per char)
            return Convert.ToInt32(result.Scalar!, System.Globalization.CultureInfo.InvariantCulture) / 2;
        }

        private int CountRows(string tableName)
        {
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            var spec = new DbCommandSpec(DbCommandKind.Scalar, $"SELECT COUNT(*) FROM [{tableName}]");
            var result = dbAccess.Execute(spec);
            return Convert.ToInt32(result.Scalar!, System.Globalization.CultureInfo.InvariantCulture);
        }

        private string GetColumnTypeName(string tableName, string columnName)
        {
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT TYPE_NAME(system_type_id) FROM sys.columns WHERE object_id = OBJECT_ID({0}) AND name = {1}",
                tableName, columnName);
            var result = dbAccess.Execute(spec);
            return Convert.ToString(result.Scalar!, System.Globalization.CultureInfo.InvariantCulture)!;
        }

        private int GetColumnScale(string tableName, string columnName)
        {
            var dbAccess = _fx.NewDbAccess(DatabaseId);
            var spec = new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT scale FROM sys.columns WHERE object_id = OBJECT_ID({0}) AND name = {1}",
                tableName, columnName);
            var result = dbAccess.Execute(spec);
            return Convert.ToInt32(result.Scalar!, System.Globalization.CultureInfo.InvariantCulture);
        }

        private UpgradePlan PlanFor(string tableName, TableSchema define, UpgradeOptions? options = null)
        {
            var provider = new Providers.SqlServer.SqlTableSchemaProvider(DatabaseId, _fx.GetRequiredService<IDbConnectionManager>());
            var real = provider.GetTableSchema(tableName);
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();
            return CreateOrchestrator().Plan(diff, options);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: an empty plan returns false")]
        public void Execute_EmptyPlan_ReturnsFalse()
        {
            var plan = new UpgradePlan(UpgradeExecutionMode.NoChange);
            var executed = CreateOrchestrator().Execute(plan, DatabaseId);
            Assert.False(executed);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: a new table is created in the database in Create mode")]
        public void Execute_NewTable_CreatesTable()
        {
            const string tableName = "st_orch_create_test";
            DropIfExists(tableName);
            try
            {
                var plan = PlanFor(tableName, BuildSchema(tableName));

                Assert.Equal(UpgradeExecutionMode.Create, plan.Mode);
                var executed = CreateOrchestrator().Execute(plan, DatabaseId);

                Assert.True(executed);
                Assert.True(TableExists(tableName));
            }
            finally
            {
                DropIfExists(tableName);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: adding a field takes the ALTER path and keeps the existing data")]
        public void Execute_AddColumn_PreservesExistingData()
        {
            const string tableName = "st_orch_addcol_test";
            DropIfExists(tableName);
            try
            {
                var initial = BuildSchema(tableName);
                CreateOrchestrator().Execute(PlanFor(tableName, initial), DatabaseId);
                var dbAccess = _fx.NewDbAccess(DatabaseId);
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"INSERT INTO [{tableName}] (sys_rowid, name) VALUES (NEWID(), {{0}})", "Alice"));

                var updated = BuildSchema(tableName);
                updated.Fields!.Add("age", "Age", FieldDbType.Integer);
                var plan = PlanFor(tableName, updated);

                Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
                CreateOrchestrator().Execute(plan, DatabaseId);

                Assert.True(ColumnExists(tableName, "age"));
                Assert.Equal(1, CountRows(tableName));
            }
            finally
            {
                DropIfExists(tableName);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: widening a field length takes the ALTER path")]
        public void Execute_WidenColumnLength_UsesAlterPath()
        {
            const string tableName = "st_orch_widen_test";
            DropIfExists(tableName);
            try
            {
                var initial = BuildSchema(tableName, nameLength: 50);
                CreateOrchestrator().Execute(PlanFor(tableName, initial), DatabaseId);

                var widened = BuildSchema(tableName, nameLength: 100);
                var plan = PlanFor(tableName, widened);

                Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
                CreateOrchestrator().Execute(plan, DatabaseId);

                Assert.Equal(100, GetColumnLength(tableName, "name"));
            }
            finally
            {
                DropIfExists(tableName);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: a cross-family type change takes the Rebuild path (whether data is lost depends on the data)")]
        public void Execute_CrossFamilyTypeChange_UsesRebuildPath()
        {
            const string tableName = "st_orch_rebuild_test";
            DropIfExists(tableName);
            try
            {
                var initial = BuildSchema(tableName);
                CreateOrchestrator().Execute(PlanFor(tableName, initial), DatabaseId);

                // Insert a numeric-looking string so the CAST during the rebuild still succeeds.
                var dbAccess = _fx.NewDbAccess(DatabaseId);
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"INSERT INTO [{tableName}] (sys_rowid, name) VALUES (NEWID(), '42')"));

                var updated = BuildSchema(tableName);
                updated.Fields!["name"].DbType = FieldDbType.Integer;
                updated.Fields!["name"].Length = 0;

                var plan = PlanFor(tableName, updated);
                Assert.Equal(UpgradeExecutionMode.Rebuild, plan.Mode);

                CreateOrchestrator().Execute(plan, DatabaseId);

                // The row still exists (the value was converted).
                Assert.Equal(1, CountRows(tableName));
            }
            finally
            {
                DropIfExists(tableName);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: rerunning the same definition gives NoChange (idempotent)")]
        public void Execute_SameSchemaTwice_IsIdempotent()
        {
            const string tableName = "st_orch_idempotent_test";
            DropIfExists(tableName);
            try
            {
                var define = BuildSchema(tableName);
                CreateOrchestrator().Execute(PlanFor(tableName, define), DatabaseId);

                var plan2 = PlanFor(tableName, define);

                Assert.Equal(UpgradeExecutionMode.NoChange, plan2.Mode);
                Assert.False(CreateOrchestrator().Execute(plan2, DatabaseId));
            }
            finally
            {
                DropIfExists(tableName);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: an existing datetime field is upgraded to datetime2(7) through ALTER and the data is kept")]
        public void Execute_LegacyDatetimeColumn_UpgradesToDatetime2()
        {
            const string tableName = "st_orch_dt2_test";
            DropIfExists(tableName);
            try
            {
                // Create a table with a legacy `datetime` column via raw SQL (the framework now
                // emits datetime2, so the legacy state must be constructed directly).
                var dbAccess = _fx.NewDbAccess(DatabaseId);
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"CREATE TABLE [{tableName}] (" +
                    "[sys_rowid] [uniqueidentifier] NOT NULL DEFAULT (newid()) PRIMARY KEY, " +
                    "[created_at] [datetime] NOT NULL DEFAULT (getutcdate()));"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"INSERT INTO [{tableName}] (sys_rowid, created_at) VALUES (NEWID(), '2026-07-02T10:00:00');"));

                Assert.Equal("datetime", GetColumnTypeName(tableName, "created_at"));

                // Defined schema declares the column as DateTime → forward map is datetime2(7).
                var define = new TableSchema { TableName = tableName };
                define.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
                define.Fields!.Add("created_at", "Created", FieldDbType.DateTime);
                define.Indexes!.AddPrimaryKey("sys_rowid");

                var plan = PlanFor(tableName, define);
                Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
                CreateOrchestrator().Execute(plan, DatabaseId);

                // Column upgraded to datetime2(7); existing row preserved.
                Assert.Equal("datetime2", GetColumnTypeName(tableName, "created_at"));
                Assert.Equal(7, GetColumnScale(tableName, "created_at"));
                Assert.Equal(1, CountRows(tableName));

                // Re-planning against the same definition now converges to NoChange.
                Assert.Equal(UpgradeExecutionMode.NoChange, PlanFor(tableName, define).Mode);
            }
            finally
            {
                DropIfExists(tableName);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Integration: a real-only field (extension) is kept after a rebuild")]
        public void Execute_RebuildPath_PreservesExtensionField()
        {
            const string tableName = "st_orch_extfield_test";
            DropIfExists(tableName);
            try
            {
                // After creating the table, add a "third-party" field legacy_col by hand.
                var initial = BuildSchema(tableName);
                CreateOrchestrator().Execute(PlanFor(tableName, initial), DatabaseId);
                var dbAccess = _fx.NewDbAccess(DatabaseId);
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"ALTER TABLE [{tableName}] ADD [legacy_col] [nvarchar](10) NULL;"));

                // Trigger a rebuild (a cross-family change).
                var updated = BuildSchema(tableName);
                updated.Fields!["name"].DbType = FieldDbType.Integer;
                updated.Fields!["name"].Length = 0;

                var plan = PlanFor(tableName, updated);
                Assert.Equal(UpgradeExecutionMode.Rebuild, plan.Mode);

                CreateOrchestrator().Execute(plan, DatabaseId);

                Assert.True(ColumnExists(tableName, "legacy_col"));
            }
            finally
            {
                DropIfExists(tableName);
            }
        }
    }
}
