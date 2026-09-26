using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Ddl;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Db.Providers;
using Polhem.Db.Providers.SqlServer;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class TableUpgradeOrchestratorTests : IClassFixture<SharedDbFixture>
    {
        public TableUpgradeOrchestratorTests(SharedDbFixture _) { }

        private static readonly IDialectFactory s_dialect = new SqlDialectFactory();
        private static readonly IDbConnectionManager s_stubMgr = new StubConnectionManager();

        private static TableSchema BuildDefineSchema(string tableName = "st_demo")
        {
            var schema = new TableSchema { TableName = tableName };
            schema.Fields!.Add("id", "Id", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            schema.Indexes!.AddPrimaryKey("id");
            return schema;
        }

        private static TableSchema BuildRealSchema(string tableName = "st_demo")
        {
            var schema = new TableSchema { TableName = tableName };
            schema.Fields!.Add("id", "Id", FieldDbType.Guid);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 50);
            var pk = new DbTableIndex { Name = $"pk_{tableName}", PrimaryKey = true, Unique = true };
            pk.IndexFields!.Add("id");
            schema.Indexes!.Add(pk);
            return schema;
        }

        private static UpgradePlan PlanFor(TableSchema define, TableSchema? real, UpgradeOptions? options = null)
        {
            var diff = new TableSchemaComparer(define, real, DatabaseType.SQLServer).CompareToDiff();
            return new TableUpgradeOrchestrator(s_dialect, s_stubMgr).Plan(diff, options);
        }

        [Fact]
        [DisplayName("Plan for a new table uses Create mode with a single CreateTable stage")]
        public void Plan_NewTable_ReturnsCreateMode()
        {
            var plan = PlanFor(BuildDefineSchema(), real: null);

            Assert.Equal(UpgradeExecutionMode.Create, plan.Mode);
            Assert.False(plan.IsEmpty);
            var stage = Assert.Single(plan.Stages);
            Assert.Equal(UpgradeStageKind.CreateTable, stage.Kind);
        }

        [Fact]
        [DisplayName("Plan for identical structures uses NoChange mode")]
        public void Plan_IdenticalSchemas_ReturnsNoChange()
        {
            var plan = PlanFor(BuildDefineSchema(), BuildRealSchema());

            Assert.Equal(UpgradeExecutionMode.NoChange, plan.Mode);
            Assert.True(plan.IsEmpty);
            Assert.Empty(plan.Stages);
        }

        [Fact]
        [DisplayName("Plan uses Alter mode when ALTER can handle every change")]
        public void Plan_AlterCapableChanges_ReturnsAlterMode()
        {
            var define = BuildDefineSchema();
            define.Fields!.Add("age", "Age", FieldDbType.Integer);
            var plan = PlanFor(define, BuildRealSchema());

            Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
            Assert.NotEmpty(plan.Stages);
        }

        [Fact]
        [DisplayName("Plan falls back to Rebuild mode for the whole table when a change needs Rebuild")]
        public void Plan_AnyRebuildChange_FallsBackToRebuildMode()
        {
            // Changing name from String to Integer (across families) requires a Rebuild.
            var define = BuildDefineSchema();
            define.Fields!["name"].DbType = FieldDbType.Integer;
            define.Fields!["name"].Length = 0;

            var plan = PlanFor(define, BuildRealSchema());

            Assert.Equal(UpgradeExecutionMode.Rebuild, plan.Mode);
            var stage = Assert.Single(plan.Stages);
            Assert.Equal(UpgradeStageKind.Rebuild, stage.Kind);
        }

        [Fact]
        [DisplayName("Plan throws for a Rebuild combined with a rename")]
        public void Plan_RebuildWithRename_Throws()
        {
            var define = BuildDefineSchema();
            define.Fields!["name"].FieldName = "display_name";
            define.Fields!["display_name"].OriginalFieldName = "name";
            // Also change the new field's type across families (which triggers a rebuild).
            define.Fields!["display_name"].DbType = FieldDbType.Integer;
            define.Fields!["display_name"].Length = 0;

            var diff = new TableSchemaComparer(define, BuildRealSchema(), DatabaseType.SQLServer).CompareToDiff();
            var orchestrator = new TableUpgradeOrchestrator(s_dialect, s_stubMgr);

            Assert.Throws<InvalidOperationException>(() => orchestrator.Plan(diff));
        }

        [Fact]
        [DisplayName("Plan orders the ALTER stages DropIndexes → AlterColumns → AddColumns → CreateIndexes → SyncDescriptions")]
        public void Plan_AlterMode_StagesAreOrdered()
        {
            var define = BuildDefineSchema();
            // The existing PK index definition differs (Unique is false), which produces a drop + add index.
            define.Indexes!["pk_{0}"].Name = "pk_{0}"; // Keeps the PK.
            define.Fields!["name"].Length = 100;       // Triggers an alter column.
            define.Fields!.Add("age", "Age", FieldDbType.Integer); // Triggers an add column.
            define.Indexes!.Add("ix_{0}_name", "name", false); // Triggers a create index.
            define.DisplayName = "示範";                // Triggers a description sync.
            define.Fields!["name"].Caption = "新名稱";

            var real = BuildRealSchema();
            // Changing Unique on the real PK triggers DropIndex + AddIndex for the PK.
            real.Indexes!["pk_st_demo"].Unique = false;

            var plan = PlanFor(define, real);

            Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
            var kinds = plan.Stages.Select(s => s.Kind).ToList();
            // All these stages must be present; check their relative order.
            int dropIdx = kinds.IndexOf(UpgradeStageKind.DropIndexes);
            int alterIdx = kinds.IndexOf(UpgradeStageKind.AlterColumns);
            int addIdx = kinds.IndexOf(UpgradeStageKind.AddColumns);
            int createIdx = kinds.IndexOf(UpgradeStageKind.CreateIndexes);
            int syncIdx = kinds.IndexOf(UpgradeStageKind.SyncDescriptions);
            Assert.True(dropIdx >= 0);
            Assert.True(alterIdx > dropIdx);
            Assert.True(addIdx > alterIdx);
            Assert.True(createIdx > addIdx);
            Assert.True(syncIdx > createIdx);
        }

        [Fact]
        [DisplayName("Plan for a description-only difference produces only the SyncDescriptions stage")]
        public void Plan_DescriptionOnly_EmitsOnlySyncDescriptionsStage()
        {
            var define = BuildDefineSchema();
            define.DisplayName = "示範";
            var plan = PlanFor(define, BuildRealSchema());

            Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
            var stage = Assert.Single(plan.Stages);
            Assert.Equal(UpgradeStageKind.SyncDescriptions, stage.Kind);
        }

        [Fact]
        [DisplayName("Plan throws for a narrowing change under the default options")]
        public void Plan_NarrowingDisallowed_Throws()
        {
            var define = BuildDefineSchema();
            define.Fields!["name"].Length = 30; // Narrowed from 50 in the real table to 30.

            var diff = new TableSchemaComparer(define, BuildRealSchema(), DatabaseType.SQLServer).CompareToDiff();
            var orchestrator = new TableUpgradeOrchestrator(s_dialect, s_stubMgr);

            Assert.Throws<InvalidOperationException>(() => orchestrator.Plan(diff));
        }

        [Fact]
        [DisplayName("Plan allows a narrowing change with AllowColumnNarrowing=true and produces a warning")]
        public void Plan_NarrowingAllowed_ReturnsPlanWithWarning()
        {
            var define = BuildDefineSchema();
            define.Fields!["name"].Length = 30;

            var diff = new TableSchemaComparer(define, BuildRealSchema(), DatabaseType.SQLServer).CompareToDiff();
            var options = new UpgradeOptions { AllowColumnNarrowing = true };
            var plan = new TableUpgradeOrchestrator(s_dialect, s_stubMgr).Plan(diff, options);

            Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
            Assert.NotEmpty(plan.Warnings);
            Assert.Contains(plan.Warnings, w => w.Contains("Narrowing"));
        }

        [Fact]
        [DisplayName("Plan throws for a NotSupported change")]
        public void Plan_NotSupportedChange_Throws()
        {
            var orchestrator = new TableUpgradeOrchestrator(new NotSupportedDialect(), new StubConnectionManager());
            var define = BuildDefineSchema();
            define.Fields!.Add("age", "Age", FieldDbType.Integer);
            var diff = new TableSchemaComparer(define, BuildRealSchema(), DatabaseType.SQLServer).CompareToDiff();

            Assert.Throws<InvalidOperationException>(() => orchestrator.Plan(diff));
        }

        // A test dialect: the alter builder always returns NotSupported,
        // and everything else delegates to `SqlDialectFactory`.
        private sealed class NotSupportedDialect : IDialectFactory
        {
            private readonly SqlDialectFactory _inner = new();
            public ITableSchemaProvider CreateTableSchemaProvider(string databaseId, IDbConnectionManager connectionManager)
                => _inner.CreateTableSchemaProvider(databaseId, connectionManager);
            public ICreateTableCommandBuilder CreateCreateTableCommandBuilder() => _inner.CreateCreateTableCommandBuilder();
            public ITableAlterCommandBuilder CreateTableAlterCommandBuilder() => new NotSupportedBuilder();
            public ITableRebuildCommandBuilder CreateTableRebuildCommandBuilder() => _inner.CreateTableRebuildCommandBuilder();
            public IFormCommandBuilder CreateFormCommandBuilder(FormSchema formDefine, IDefineAccess defineAccess) => _inner.CreateFormCommandBuilder(formDefine, defineAccess);
            public string GetDefaultValueExpression(FieldDbType dbType) => _inner.GetDefaultValueExpression(dbType);
        }

        // Stub connection manager — used solely so TableUpgradeOrchestrator can construct;
        // the NotSupported path throws before any connection method is invoked.
        private sealed class StubConnectionManager : IDbConnectionManager
        {
            public DbConnectionInfo GetConnectionInfo(string databaseId) => throw new NotSupportedException();
            public System.Data.Common.DbConnection CreateConnection(string databaseId) => throw new NotSupportedException();
            public bool Remove(string databaseId) => false;
            public void Clear() { }
            public bool Contains(string databaseId) => false;
            public int Count => 0;
        }

        // A custom alter builder whose `GetExecutionKind` always returns NotSupported,
        // to verify that the orchestrator rejects the change.
        private sealed class NotSupportedBuilder : ITableAlterCommandBuilder
        {
            public ChangeExecutionKind GetExecutionKind(ITableChange change) => ChangeExecutionKind.NotSupported;
            public bool IsNarrowingChange(ITableChange change) => false;
            public IReadOnlyList<string> GetStatements(string tableName, ITableChange change) => [];
        }
    }
}
