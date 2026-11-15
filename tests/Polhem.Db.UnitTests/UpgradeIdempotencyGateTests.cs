using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// After an upgrade to a definition, comparing the same definition again must find no difference. A difference that
    /// survives its own upgrade is planned again on every start-up: an endless upgrade, which on SQLite rebuilds the
    /// table each time.
    /// </summary>
    /// <remarks>
    /// The definition covers every column type, nullable and with literal defaults, a secondary and a unique index, and
    /// descriptions. Each database reaches it by the three paths an upgrade takes: creating the table, adding columns
    /// to a table with a row (and widening one), and rebuilding the table.
    /// </remarks>
    public class UpgradeIdempotencyGateTests : IClassFixture<SharedDbFixture>
    {
        internal const string TableName = "idem_gate_test";

        private readonly SharedDbFixture _fx;

        public UpgradeIdempotencyGateTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a created table compares equal to its definition")]
        public void Create_SqlServer_IsIdempotent() => RunCreate(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a created table compares equal to its definition")]
        public void Create_PostgreSql_IsIdempotent() => RunCreate(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a created table compares equal to its definition")]
        public void Create_MySql_IsIdempotent() => RunCreate(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a created table compares equal to its definition")]
        public void Create_Oracle_IsIdempotent() => RunCreate(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a created table compares equal to its definition")]
        public void Create_Sqlite_IsIdempotent() => RunCreate(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a table upgraded by adding and widening columns compares equal to its definition")]
        public void AddColumns_SqlServer_IsIdempotent() => RunAddColumns(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a table upgraded by adding and widening columns compares equal to its definition")]
        public void AddColumns_PostgreSql_IsIdempotent() => RunAddColumns(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a table upgraded by adding and widening columns compares equal to its definition")]
        public void AddColumns_MySql_IsIdempotent() => RunAddColumns(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a table upgraded by adding and widening columns compares equal to its definition")]
        public void AddColumns_Oracle_IsIdempotent() => RunAddColumns(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a table upgraded by adding and widening columns compares equal to its definition")]
        public void AddColumns_Sqlite_IsIdempotent() => RunAddColumns(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a rebuilt table compares equal to its definition")]
        public void Rebuild_SqlServer_IsIdempotent() => RunRebuild(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a rebuilt table compares equal to its definition")]
        public void Rebuild_PostgreSql_IsIdempotent() => RunRebuild(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a rebuilt table compares equal to its definition")]
        public void Rebuild_MySql_IsIdempotent() => RunRebuild(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a rebuilt table compares equal to its definition")]
        public void Rebuild_Oracle_IsIdempotent() => RunRebuild(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a rebuilt table compares equal to its definition")]
        public void Rebuild_Sqlite_IsIdempotent() => RunRebuild(DatabaseType.SQLite);

        private void RunCreate(DatabaseType databaseType)
            => WithTable(databaseType, databaseId =>
            {
                Upgrade(databaseId, BuildDefinition());
                AssertIdempotent(databaseId);
            });

        private void RunAddColumns(DatabaseType databaseType)
            => WithTable(databaseType, databaseId =>
            {
                Upgrade(databaseId, BuildAddColumnsStart());
                _fx.NewDbAccess(databaseId).Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "INSERT INTO idem_gate_test (s, bin) VALUES ({0}, {1})", "row", new byte[] { 1, 2 }));

                Upgrade(databaseId, BuildDefinition());

                AssertIdempotent(databaseId);
            });

        private void RunRebuild(DatabaseType databaseType)
            => WithTable(databaseType, databaseId =>
            {
                Upgrade(databaseId, BuildRebuildStart());

                var plan = Plan(databaseId, BuildDefinition());
                Assert.Equal(UpgradeExecutionMode.Rebuild, plan.Mode);
                Orchestrator().Execute(plan, databaseId);

                AssertIdempotent(databaseId);
            });

        // Columns the add path brings to a table that already has a row; the others exist from the start.
        private static readonly string[] s_addedColumns =
        [
            "s_def", "t", "b_def", "i_def", "d_def", "dt", "dtm", "tm", "g", "dt_null", "i_null", "g_null",
        ];

        /// <summary>
        /// The table before the add path: without the added columns and the unique index, and with a narrower column.
        /// </summary>
        internal static TableSchema BuildAddColumnsStart()
        {
            var before = BuildDefinition();
            foreach (var name in s_addedColumns)
                before.Fields!.Remove(before.Fields[name]);
            before.Indexes!.Remove(before.Indexes["uk_{0}_i_def"]);
            before.Fields!["s"].Length = 20;
            return before;
        }

        /// <summary>
        /// The table before the rebuild path: a change across type families (number to text) rebuilds the table on
        /// every database.
        /// </summary>
        internal static TableSchema BuildRebuildStart()
        {
            var before = BuildDefinition();
            before.Fields!["code"].DbType = FieldDbType.Integer;
            before.Fields!["code"].Length = 0;
            return before;
        }

        internal static TableSchema BuildDefinition()
        {
            var schema = new TableSchema { TableName = TableName, DisplayName = "Idempotency gate" };
            var f = schema.Fields!;
            // On SQLite an AutoIncrement column has to be the single-column primary key.
            f.Add("sys_no", "Number", FieldDbType.AutoIncrement);
            f.Add("s", "Text up to 30", FieldDbType.String, 30);
            f.Add("s_def", "Text with a default", FieldDbType.String, 30).DefaultValue = "x";
            f.Add("s_null", "Nullable text", FieldDbType.String, 30).AllowNull = true;
            f.Add("code", "Code", FieldDbType.String, 10);
            f.Add("t", "Long text", FieldDbType.Text);
            f.Add("t_null", "Nullable long text", FieldDbType.Text).AllowNull = true;
            f.Add("b", "Flag", FieldDbType.Boolean);
            f.Add("b_def", "Flag set by default", FieldDbType.Boolean).DefaultValue = "1";
            f.Add("sh", "Short", FieldDbType.Short);
            f.Add("i", "Integer", FieldDbType.Integer);
            f.Add("i_def", "Integer with a default", FieldDbType.Integer).DefaultValue = "7";
            f.Add("i_null", "Nullable integer", FieldDbType.Integer).AllowNull = true;
            f.Add("l", "Long", FieldDbType.Long);
            var d = f.Add("d", "Decimal", FieldDbType.Decimal);
            d.Precision = 18;
            d.Scale = 4;
            var dDef = f.Add("d_def", "Decimal with a default", FieldDbType.Decimal);
            dDef.Precision = 10;
            dDef.Scale = 2;
            dDef.DefaultValue = "1.5";
            f.Add("c", "Currency", FieldDbType.Currency);
            f.Add("dt", "Date", FieldDbType.Date);
            f.Add("dt_null", "Nullable date", FieldDbType.Date).AllowNull = true;
            f.Add("dtm", "Date and time", FieldDbType.DateTime);
            f.Add("tm", "Time of day", FieldDbType.Time);
            f.Add("g", "Guid", FieldDbType.Guid);
            f.Add("g_null", "Nullable guid", FieldDbType.Guid).AllowNull = true;
            f.Add("bin", "Bytes", FieldDbType.Binary);
            schema.Indexes!.AddPrimaryKey("sys_no");
            schema.Indexes!.Add("ix_{0}_s", "s", false);
            schema.Indexes!.Add("uk_{0}_i_def", "i_def,sys_no", true);
            return schema;
        }

        private void AssertIdempotent(string databaseId)
        {
            var diff = Diff(databaseId, BuildDefinition());
            var left = diff.Changes.Select(Describe)
                .Concat(diff.DescriptionChanges.Select(c => $"description {c.Level} {c.FieldName}: '{c.NewValue}'"))
                .ToList();
            Assert.True(left.Count == 0, "The upgrade left differences: " + string.Join("; ", left));
            Assert.Equal(UpgradeExecutionMode.NoChange, Orchestrator().Plan(diff).Mode);
        }

        private DatabaseType _databaseType;

        private static string Describe(Polhem.Db.Schema.Changes.ITableChange change)
            => change is Polhem.Db.Schema.Changes.AlterFieldChange alter
                ? $"{change.Describe()} (database {Shape(alter.OldField)}, definition {Shape(alter.NewField)})"
                : change.Describe();

        private static string Shape(DbField field)
            => $"{field.DbType} length {field.Length} precision {field.Precision} scale {field.Scale} " +
               $"null {field.AllowNull} default '{field.DefaultValue}'";

        private TableUpgradeOrchestrator Orchestrator()
            => new(DbDialectRegistry.Get(_databaseType), _fx.GetRequiredService<IDbConnectionManager>());

        private TableSchemaDiff Diff(string databaseId, TableSchema define)
        {
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var real = DbDialectRegistry.Get(_databaseType)
                .CreateTableSchemaProvider(databaseId, connectionManager)
                .GetTableSchema(TableName);
            return new TableSchemaComparer(define, real, _databaseType).CompareToDiff();
        }

        private UpgradePlan Plan(string databaseId, TableSchema define)
            => Orchestrator().Plan(Diff(databaseId, define));

        private void Upgrade(string databaseId, TableSchema define)
            => Orchestrator().Execute(Plan(databaseId, define), databaseId);

        private void WithTable(DatabaseType databaseType, Action<string> test)
        {
            _databaseType = databaseType;
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, databaseType);
            try
            {
                test(databaseId);
            }
            finally
            {
                Drop(dbAccess, databaseType);
            }
        }

        private static void Drop(DbAccess dbAccess, DatabaseType databaseType)
        {
            // Oracle has no DROP TABLE IF EXISTS, so a PL/SQL block swallows ORA-00942 instead.
            string sql = databaseType switch
            {
                DatabaseType.SQLServer => "IF OBJECT_ID(N'idem_gate_test', N'U') IS NOT NULL DROP TABLE idem_gate_test;",
                DatabaseType.Oracle =>
                    "BEGIN EXECUTE IMMEDIATE 'DROP TABLE IDEM_GATE_TEST CASCADE CONSTRAINTS'; " +
                    "EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; END;",
                _ => "DROP TABLE IF EXISTS idem_gate_test",
            };
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql));
        }
    }
}
