using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// A table created before schema format 2 gave every GUID column a generated default. The upgrade must move a
    /// reference column to the empty GUID, keep the generated default of the row id, keep the values already stored,
    /// and converge. Adding the row id to a table that already has rows must give each row its own id.
    /// </summary>
    public class GuidDefaultUpgradeDbTests : IClassFixture<SharedDbFixture>
    {
        private const string TableName = "guid_default_test";

        private readonly SharedDbFixture _fx;
        private DatabaseType _databaseType;

        public GuidDefaultUpgradeDbTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a reference column's generated default is upgraded to the empty GUID")]
        public void LegacyDefault_SqlServer_UpgradesToEmptyGuid() => Run(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a reference column's generated default is upgraded to the empty GUID")]
        public void LegacyDefault_PostgreSql_UpgradesToEmptyGuid() => Run(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a reference column's generated default is upgraded to the empty GUID")]
        public void LegacyDefault_MySql_UpgradesToEmptyGuid() => Run(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a reference column's generated default is upgraded to the empty GUID")]
        public void LegacyDefault_Oracle_UpgradesToEmptyGuid() => Run(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a reference column's generated default is upgraded to the empty GUID")]
        public void LegacyDefault_Sqlite_UpgradesToEmptyGuid() => Run(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: adding the row id to a table with rows gives each row its own id")]
        public void AddRowId_SqlServer_GivesEachRowItsOwnId() => RunAddRowId(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: adding the row id to a table with rows gives each row its own id")]
        public void AddRowId_PostgreSql_GivesEachRowItsOwnId() => RunAddRowId(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: adding the row id to a table with rows gives each row its own id")]
        public void AddRowId_MySql_GivesEachRowItsOwnId() => RunAddRowId(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: adding the row id to a table with rows gives each row its own id")]
        public void AddRowId_Oracle_GivesEachRowItsOwnId() => RunAddRowId(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: adding the row id to a table with rows gives each row its own id")]
        public void AddRowId_Sqlite_GivesEachRowItsOwnId() => RunAddRowId(DatabaseType.SQLite);

        private static readonly string[] s_rows = ["a", "b", "c"];

        private void RunAddRowId(DatabaseType databaseType)
        {
            _databaseType = databaseType;
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, databaseType);
            try
            {
                var withoutRowId = BuildDefinition();
                withoutRowId.Fields!.Remove(withoutRowId.Fields["sys_rowid"]);
                Upgrade(databaseId, withoutRowId);
                foreach (var row in s_rows) { Insert(dbAccess, row); }

                Upgrade(databaseId, BuildDefinition());

                var rowIds = s_rows.Select(row => ReadRowId(dbAccess, row)).ToList();
                Assert.DoesNotContain(Guid.Empty, rowIds);
                Assert.Equal(s_rows.Length, rowIds.Distinct().Count());
                Assert.Empty(Diff(databaseId).Changes);
            }
            finally
            {
                Drop(dbAccess, databaseType);
            }
        }

        private void Upgrade(string databaseId, TableSchema define)
        {
            var real = DbDialectRegistry.Get(_databaseType)
                .CreateTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>())
                .GetTableSchema(TableName);
            var diff = new TableSchemaComparer(define, real, _databaseType).CompareToDiff();
            Orchestrator().Execute(Orchestrator().Plan(diff), databaseId);
        }

        private void Run(DatabaseType databaseType)
        {
            _databaseType = databaseType;
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, databaseType);
            try
            {
                CreateWithLegacyDefaults(databaseId);
                Insert(dbAccess, "before");
                var storedBefore = ReadRefRowId(dbAccess, "before");
                Assert.NotEqual(Guid.Empty, storedBefore);

                var diff = Diff(databaseId);
                var changed = diff.Changes.OfType<AlterFieldChange>().Select(c => c.NewField.FieldName).ToList();
                Assert.Equal(["ref_rowid"], changed);

                Orchestrator().Execute(Orchestrator().Plan(diff), databaseId);

                var after = Diff(databaseId);
                Assert.Empty(after.Changes);
                Assert.Equal(UpgradeExecutionMode.NoChange, Orchestrator().Plan(after).Mode);

                Assert.Equal(storedBefore, ReadRefRowId(dbAccess, "before"));
                Insert(dbAccess, "after");
                Assert.Equal(Guid.Empty, ReadRefRowId(dbAccess, "after"));
                Assert.NotEqual(Guid.Empty, ReadRowId(dbAccess, "after"));
            }
            finally
            {
                Drop(dbAccess, databaseType);
            }
        }

        private static TableSchema BuildDefinition()
        {
            var schema = new TableSchema { TableName = TableName };
            var f = schema.Fields!;
            f.Add("sys_no", string.Empty, FieldDbType.AutoIncrement);
            f.Add("sys_rowid", string.Empty, FieldDbType.Guid);
            f.Add("ref_rowid", string.Empty, FieldDbType.Guid);
            f.Add("s", string.Empty, FieldDbType.String, 10);
            schema.Indexes!.AddPrimaryKey("sys_no");
            return schema;
        }

        // Creates the table with the DDL of the current format, then puts back the generated default each dialect gave
        // the reference column under schema format 1.
        private void CreateWithLegacyDefaults(string databaseId)
        {
            var plan = Orchestrator().Plan(new TableSchemaComparer(BuildDefinition(), null, _databaseType).CompareToDiff());
            var (current, legacy) = _databaseType switch
            {
                DatabaseType.SQLServer => ("'00000000-0000-0000-0000-000000000000'", "newid()"),
                DatabaseType.PostgreSQL => ("'00000000-0000-0000-0000-000000000000'", "gen_random_uuid()"),
                DatabaseType.MySQL => ("'00000000-0000-0000-0000-000000000000'", "(UUID())"),
                DatabaseType.Oracle => ("HEXTORAW(''00000000000000000000000000000000'')", "SYS_GUID()"),
                _ => ("'00000000-0000-0000-0000-000000000000'", "(hex(randomblob(16)))"),
            };
            var stages = plan.Stages.Select(stage => new UpgradeStage(stage.Kind,
                stage.Statements.Select(statement => statement.Replace(current, legacy, StringComparison.Ordinal))));
            var legacyPlan = new UpgradePlan(plan.Mode, stages) { DefineTable = plan.DefineTable };
            Assert.Contains(legacyPlan.Stages.SelectMany(stage => stage.Statements),
                statement => statement.Contains(legacy, StringComparison.Ordinal));
            Orchestrator().Execute(legacyPlan, databaseId);
        }

        private static void Insert(DbAccess dbAccess, string s)
            => dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO guid_default_test (s) VALUES ({0})", s));

        private static Guid ReadRefRowId(DbAccess dbAccess, string s) => ReadGuid(dbAccess, "ref_rowid", s);

        private static Guid ReadRowId(DbAccess dbAccess, string s) => ReadGuid(dbAccess, "sys_rowid", s);

        private static Guid ReadGuid(DbAccess dbAccess, string column, string s)
        {
            var value = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                $"SELECT {column} FROM guid_default_test WHERE s = {{0}}", s)).Scalar;
            Assert.NotNull(value);
            return ValueUtilities.CGuid(value);
        }

        private TableUpgradeOrchestrator Orchestrator()
            => new(DbDialectRegistry.Get(_databaseType), _fx.GetRequiredService<IDbConnectionManager>());

        private TableSchemaDiff Diff(string databaseId)
        {
            var real = DbDialectRegistry.Get(_databaseType)
                .CreateTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>())
                .GetTableSchema(TableName);
            return new TableSchemaComparer(BuildDefinition(), real, _databaseType).CompareToDiff();
        }

        private static void Drop(DbAccess dbAccess, DatabaseType databaseType)
        {
            // Oracle has no DROP TABLE IF EXISTS, so a PL/SQL block swallows ORA-00942 instead.
            string sql = databaseType switch
            {
                DatabaseType.SQLServer => "IF OBJECT_ID(N'guid_default_test', N'U') IS NOT NULL DROP TABLE guid_default_test;",
                DatabaseType.Oracle =>
                    "BEGIN EXECUTE IMMEDIATE 'DROP TABLE GUID_DEFAULT_TEST CASCADE CONSTRAINTS'; " +
                    "EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; END;",
                _ => "DROP TABLE IF EXISTS guid_default_test",
            };
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql));
        }
    }
}
