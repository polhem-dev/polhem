using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Every upgrade the orchestrator runs is recorded in the database it changes: the record is written before any
    /// statement runs and gets the outcome afterwards, and a database that a tool of a higher schema format has upgraded
    /// is refused before anything changes.
    /// </summary>
    /// <remarks>
    /// The records go to a table of their own name, so a record of a newer format written here cannot refuse the
    /// upgrades the other tests run against the same database.
    /// </remarks>
    public class TableUpgradeRecordDbTests : IClassFixture<SharedDbFixture>
    {
        private const string TableName = "upgrade_record_test";
        private const string RecordTable = "upgrade_record_test_log";

        private readonly SharedDbFixture _fx;
        private DatabaseType _databaseType;

        public TableUpgradeRecordDbTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a create and an alter each leave a succeeded record with the schema they applied")]
        public void Records_SqlServer_Succeeded() => RunSucceeded(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a create and an alter each leave a succeeded record with the schema they applied")]
        public void Records_PostgreSql_Succeeded() => RunSucceeded(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a create and an alter each leave a succeeded record with the schema they applied")]
        public void Records_MySql_Succeeded() => RunSucceeded(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a create and an alter each leave a succeeded record with the schema they applied")]
        public void Records_Oracle_Succeeded() => RunSucceeded(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a create and an alter each leave a succeeded record with the schema they applied")]
        public void Records_Sqlite_Succeeded() => RunSucceeded(DatabaseType.SQLite);

        // SQLite stores any value in any column, so converting text to a number cannot fail there.
        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a failed upgrade is recorded with its error and keeps the schema of the last success")]
        public void Records_SqlServer_Failed() => RunFailed(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a failed upgrade is recorded with its error and keeps the schema of the last success")]
        public void Records_PostgreSql_Failed() => RunFailed(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a failed upgrade is recorded with its error and keeps the schema of the last success")]
        public void Records_MySql_Failed() => RunFailed(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a failed upgrade is recorded with its error and keeps the schema of the last success")]
        public void Records_Oracle_Failed() => RunFailed(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a database upgraded by a newer schema format is refused and nothing changes")]
        public void NewerFormat_SqlServer_Refused() => RunNewerFormat(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a database upgraded by a newer schema format is refused and nothing changes")]
        public void NewerFormat_PostgreSql_Refused() => RunNewerFormat(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a database upgraded by a newer schema format is refused and nothing changes")]
        public void NewerFormat_MySql_Refused() => RunNewerFormat(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a database upgraded by a newer schema format is refused and nothing changes")]
        public void NewerFormat_Oracle_Refused() => RunNewerFormat(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a database upgraded by a newer schema format is refused and nothing changes")]
        public void NewerFormat_Sqlite_Refused() => RunNewerFormat(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a table that needed no upgrade gets a record a fast compare can rely on")]
        public void RecordUnchanged_SqlServer_FastCompareRelies() => RunRecordUnchanged(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a table that needed no upgrade gets a record a fast compare can rely on")]
        public void RecordUnchanged_PostgreSql_FastCompareRelies() => RunRecordUnchanged(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a table that needed no upgrade gets a record a fast compare can rely on")]
        public void RecordUnchanged_MySql_FastCompareRelies() => RunRecordUnchanged(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a table that needed no upgrade gets a record a fast compare can rely on")]
        public void RecordUnchanged_Oracle_FastCompareRelies() => RunRecordUnchanged(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a table that needed no upgrade gets a record a fast compare can rely on")]
        public void RecordUnchanged_Sqlite_FastCompareRelies() => RunRecordUnchanged(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("An empty plan runs nothing and does not create the record table")]
        public void EmptyPlan_DoesNotCreateRecordTable()
            => WithTables(DatabaseType.SQLite, databaseId =>
            {
                Assert.False(Orchestrator().Execute(new UpgradePlan(UpgradeExecutionMode.NoChange), databaseId));

                Assert.Null(ReadSchema(databaseId, RecordTable));
                Assert.Equal(0, Records(databaseId).GetHighestFormatVersion());
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("A plan built without its table schema runs without writing a record")]
        public void PlanWithoutDefinition_RunsUnrecorded()
            => WithTables(DatabaseType.SQLite, databaseId =>
            {
                var plan = Plan(databaseId, BuildSchema(codeType: FieldDbType.Integer));
                var unrecorded = new UpgradePlan(plan.Mode, plan.Stages, plan.Warnings);

                Assert.True(Orchestrator().Execute(unrecorded, databaseId));

                Assert.NotNull(ReadSchema(databaseId, TableName));
                Assert.Null(Records(databaseId).GetRecord(TableName));
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("TableSchemaBuilder records a table it finds current, and a fast compare then takes it as unchanged")]
        public void Builder_CurrentTable_RecordedThenTrusted()
            => WithTables(DatabaseType.SQLite, _ =>
            {
                var access = _fx.GetRequiredService<IDefineAccess>();
                var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
                var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "common");
                TableSchemaBuilder NewBuilder() => new(databaseId, access, connectionManager) { RecordTableName = RecordTable };
                var define = access.GetTableSchema("common", "st_user");
                var records = new TableUpgradeRecordStore(databaseId, connectionManager, RecordTable);
                Drop(_fx.NewDbAccess(databaseId), DatabaseType.SQLite, RecordTable);
                try
                {
                    Assert.False(NewBuilder().Execute("common", "st_user", new UpgradeOptions { CompareMode = UpgradeCompareMode.Full }));
                    Assert.True(records.IsRecordedAsCurrent(define));

                    Assert.False(NewBuilder().Execute("common", "st_user"));
                    Assert.Equal(TableUpgradeStatus.Verified, records.GetRecord("st_user")?.Status);
                }
                finally
                {
                    Drop(_fx.NewDbAccess(databaseId), DatabaseType.SQLite, RecordTable);
                }
            });

        private void RunSucceeded(DatabaseType databaseType)
            => WithTables(databaseType, databaseId =>
            {
                var created = BuildSchema(codeType: FieldDbType.Integer);
                Upgrade(databaseId, created);
                AssertSucceeded(databaseId, created);

                var altered = BuildSchema(codeType: FieldDbType.Integer, withNote: true);
                var plan = Plan(databaseId, altered);
                Assert.Equal(UpgradeExecutionMode.Alter, plan.Mode);
                Orchestrator().Execute(plan, databaseId);
                AssertSucceeded(databaseId, altered);

                Assert.Equal(SchemaFormat.Current, Records(databaseId).GetHighestFormatVersion());
                Assert.Null(Records(databaseId).DescribeNewerFormat());
                Assert.True(Diff(databaseId, altered).IsEmpty);
            });

        private void RunFailed(DatabaseType databaseType)
            => WithTables(databaseType, databaseId =>
            {
                var created = BuildSchema(codeType: FieldDbType.String);
                Upgrade(databaseId, created);
                Insert(databaseId, "not a number");

                Assert.NotNull(Record.Exception(() => Upgrade(databaseId, BuildSchema(codeType: FieldDbType.Integer))));

                var record = Records(databaseId).GetRecord(TableName)!;
                Assert.Equal(TableUpgradeStatus.Failed, record.Status);
                Assert.NotEqual(string.Empty, record.Error);
                Assert.Equal(SchemaFormat.Current, record.FormatVersion);
                Assert.Equal(XmlCodec.Serialize(created), record.SchemaXml);
                Assert.Equal(TableUpgradeRecordStore.ComputeHash(XmlCodec.Serialize(created)), record.SchemaHash);
            });

        private void RunNewerFormat(DatabaseType databaseType)
            => WithTables(databaseType, databaseId =>
            {
                var created = BuildSchema(codeType: FieldDbType.Integer);
                Upgrade(databaseId, created);
                int newer = SchemaFormat.Current + 1;
                _fx.NewDbAccess(databaseId).ExecuteNonQuery(
                    $"UPDATE {RecordTable} SET format_version = {{0}}, framework_version = {{1}} WHERE table_name = {{2}}",
                    newer, "99.0.0", TableName);
                var before = Records(databaseId).GetRecord(TableName)!;

                var plan = Plan(databaseId, BuildSchema(codeType: FieldDbType.Integer, withNote: true));
                var ex = Assert.Throws<InvalidOperationException>(() => Orchestrator().Execute(plan, databaseId));

                Assert.Contains($"schema format {newer}", ex.Message, StringComparison.Ordinal);
                Assert.Contains("99.0.0", ex.Message, StringComparison.Ordinal);
                Assert.True(Diff(databaseId, created).IsEmpty);
                var after = Records(databaseId).GetRecord(TableName)!;
                Assert.Equal(before.FormatVersion, after.FormatVersion);
                Assert.Equal(before.Status, after.Status);
                Assert.Equal(before.SchemaHash, after.SchemaHash);
                Assert.Equal(before.UpdateTime, after.UpdateTime);
            });

        private void RunRecordUnchanged(DatabaseType databaseType)
            => WithTables(databaseType, databaseId =>
            {
                var define = BuildSchema(codeType: FieldDbType.Integer);
                // Built without a record, as a table built before the records existed.
                var plan = Plan(databaseId, define);
                Assert.True(Orchestrator().Execute(new UpgradePlan(plan.Mode, plan.Stages), databaseId));
                Assert.False(Records(databaseId).IsRecordedAsCurrent(define));

                Assert.True(Records(databaseId).TryRecordUnchanged(define));

                // Nothing was changed, so the record does not refuse an older tool.
                var verified = Records(databaseId).GetRecord(TableName)!;
                Assert.Equal(TableUpgradeStatus.Verified, verified.Status);
                Assert.Equal(SchemaFormat.Current, verified.FormatVersion);
                Assert.Equal(XmlCodec.Serialize(define), verified.SchemaXml);
                Assert.Equal(0, Records(databaseId).GetHighestFormatVersion());
                var records = Records(databaseId);
                Assert.True(records.IsRecordedAsCurrent(define));
                Assert.False(records.IsRecordedAsCurrent(BuildSchema(codeType: FieldDbType.Integer, withNote: true)));

                // A failed upgrade of this format is corrected once the table is found to match, and keeps counting as
                // this format's upgrade.
                _fx.NewDbAccess(databaseId).ExecuteNonQuery(
                    $"UPDATE {RecordTable} SET status = {{0}} WHERE table_name = {{1}}", nameof(TableUpgradeStatus.Failed), TableName);
                Assert.False(Records(databaseId).IsRecordedAsCurrent(define));
                Assert.True(Records(databaseId).TryRecordUnchanged(define));
                Assert.True(Records(databaseId).IsRecordedAsCurrent(define));
                Assert.Equal(TableUpgradeStatus.Succeeded, Records(databaseId).GetRecord(TableName)!.Status);
                Assert.Equal(SchemaFormat.Current, Records(databaseId).GetHighestFormatVersion());
            });

        private void AssertSucceeded(string databaseId, TableSchema define)
        {
            var record = Records(databaseId).GetRecord(TableName);
            Assert.NotNull(record);
            Assert.Equal(TableName, record.TableName);
            Assert.Equal(TableUpgradeStatus.Succeeded, record.Status);
            Assert.Equal(string.Empty, record.Error);
            Assert.Equal(SchemaFormat.Current, record.FormatVersion);
            Assert.NotEqual(string.Empty, record.FrameworkVersion);
            Assert.Equal(XmlCodec.Serialize(define), record.SchemaXml);
            Assert.Equal(TableUpgradeRecordStore.ComputeHash(record.SchemaXml), record.SchemaHash);
            Assert.NotEqual(DateTime.MinValue, record.UpdateTime);
        }

        private static TableSchema BuildSchema(FieldDbType codeType, bool withNote = false)
        {
            var schema = new TableSchema { TableName = TableName };
            schema.Fields!.Add("sys_no", "Number", FieldDbType.AutoIncrement);
            schema.Fields!.Add("code", "Code", codeType, codeType == FieldDbType.String ? 20 : 0);
            if (withNote)
                schema.Fields!.Add("note", "Note", FieldDbType.String, 20);
            schema.Indexes!.AddPrimaryKey("sys_no");
            return schema;
        }

        private void Insert(string databaseId, object code)
            => _fx.NewDbAccess(databaseId).ExecuteNonQuery($"INSERT INTO {TableName} (code) VALUES ({{0}})", code);

        private TableUpgradeOrchestrator Orchestrator()
            => new(DbDialectRegistry.Get(_databaseType), _fx.GetRequiredService<IDbConnectionManager>())
            {
                RecordTableName = RecordTable,
            };

        private TableUpgradeRecordStore Records(string databaseId)
            => new(databaseId, _fx.GetRequiredService<IDbConnectionManager>(), RecordTable);

        private TableSchema? ReadSchema(string databaseId, string tableName)
            => DbDialectRegistry.Get(_databaseType)
                .CreateTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>())
                .GetTableSchema(tableName);

        private TableSchemaDiff Diff(string databaseId, TableSchema define)
            => new TableSchemaComparer(define, ReadSchema(databaseId, TableName), _databaseType).CompareToDiff();

        private UpgradePlan Plan(string databaseId, TableSchema define)
            => Orchestrator().Plan(Diff(databaseId, define));

        private void Upgrade(string databaseId, TableSchema define)
            => Orchestrator().Execute(Plan(databaseId, define), databaseId);

        private void WithTables(DatabaseType databaseType, Action<string> test)
        {
            _databaseType = databaseType;
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            string[] tables = [TableName, "tmp_" + TableName, RecordTable];
            foreach (var table in tables) Drop(dbAccess, databaseType, table);
            try
            {
                test(databaseId);
            }
            finally
            {
                foreach (var table in tables) Drop(dbAccess, databaseType, table);
            }
        }

        private static void Drop(DbAccess dbAccess, DatabaseType databaseType, string table)
        {
            // Oracle has no DROP TABLE IF EXISTS, so a PL/SQL block swallows ORA-00942 instead.
            string sql = databaseType switch
            {
                DatabaseType.SQLServer => $"IF OBJECT_ID(N'{table}', N'U') IS NOT NULL DROP TABLE {table};",
                DatabaseType.Oracle =>
                    $"BEGIN EXECUTE IMMEDIATE 'DROP TABLE {table.ToUpperInvariant()} CASCADE CONSTRAINTS'; " +
                    "EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; END;",
                _ => $"DROP TABLE IF EXISTS {table}",
            };
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql));
        }
    }
}
