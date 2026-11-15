using System.ComponentModel;
using System.Data;
using System.Globalization;
using Polhem.Core.Data;
using Polhem.Db.Manager;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// A table rebuild keeps what the table holds: its rows, their AutoIncrement numbers and the numbers a delete freed.
    /// When it cannot keep or finish something, it leaves the table as it was, so someone can decide what to do with
    /// the data before upgrading again.
    /// </summary>
    public class RebuildPreservationDbTests : IClassFixture<SharedDbFixture>
    {
        private const string TableName = "rebuild_keep_test";

        private readonly SharedDbFixture _fx;
        private DatabaseType _databaseType;

        public RebuildPreservationDbTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a rebuild keeps the rows and their AutoIncrement numbers")]
        public void Rebuild_SqlServer_KeepsRowsAndNumbers() => RunKeeps(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a rebuild keeps the rows and their AutoIncrement numbers")]
        public void Rebuild_PostgreSql_KeepsRowsAndNumbers() => RunKeeps(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a rebuild keeps the rows and their AutoIncrement numbers")]
        public void Rebuild_MySql_KeepsRowsAndNumbers() => RunKeeps(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a rebuild keeps the rows and their AutoIncrement numbers")]
        public void Rebuild_Oracle_KeepsRowsAndNumbers() => RunKeeps(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a rebuild keeps the rows and their AutoIncrement numbers")]
        public void Rebuild_Sqlite_KeepsRowsAndNumbers() => RunKeeps(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a rebuild that would lose a database-only index is refused and the table is left as it was")]
        public void DatabaseOnlyIndex_SqlServer_RebuildRefused() => RunDatabaseOnlyIndex(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a rebuild that would lose a database-only index is refused and the table is left as it was")]
        public void DatabaseOnlyIndex_PostgreSql_RebuildRefused() => RunDatabaseOnlyIndex(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a rebuild that would lose a database-only index is refused and the table is left as it was")]
        public void DatabaseOnlyIndex_MySql_RebuildRefused() => RunDatabaseOnlyIndex(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a rebuild that would lose a database-only index is refused and the table is left as it was")]
        public void DatabaseOnlyIndex_Oracle_RebuildRefused() => RunDatabaseOnlyIndex(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a rebuild that would lose a database-only index is refused and the table is left as it was")]
        public void DatabaseOnlyIndex_Sqlite_RebuildRefused() => RunDatabaseOnlyIndex(DatabaseType.SQLite);

        // SQLite stores any value in any column, so converting text to a number cannot fail there.
        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a rebuild whose copy fails leaves the table and its rows as they were")]
        public void FailedRebuild_SqlServer_LeavesTable() => RunFailedRebuild(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a rebuild whose copy fails leaves the table and its rows as they were")]
        public void FailedRebuild_PostgreSql_LeavesTable() => RunFailedRebuild(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a rebuild whose copy fails leaves the table and its rows as they were")]
        public void FailedRebuild_MySql_LeavesTable() => RunFailedRebuild(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a rebuild whose copy fails leaves the table and its rows as they were")]
        public void FailedRebuild_Oracle_LeavesTable() => RunFailedRebuild(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a rebuild of a table with a column of an unmapped type is refused before anything runs")]
        public void UnmappedColumn_Sqlite_RefusedAndTableLeft()
            => WithTable(DatabaseType.SQLite, (databaseId, dbAccess) =>
            {
                Upgrade(databaseId, BuildSchema(codeType: FieldDbType.Integer));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, "ALTER TABLE rebuild_keep_test ADD COLUMN payload JSON"));
                Insert(dbAccess, "kept", 1);

                var ex = Assert.Throws<InvalidOperationException>(() => Plan(databaseId, BuildSchema(codeType: FieldDbType.String)));

                Assert.Contains("'payload'", ex.Message, StringComparison.Ordinal);
                Assert.Equal(FieldDbType.Integer, ReadSchema(databaseId)!.Fields!["code"].DbType);
                Assert.Equal(["kept"], Names(dbAccess));
            });

        private void RunKeeps(DatabaseType databaseType)
            => WithTable(databaseType, (databaseId, dbAccess) =>
            {
                Upgrade(databaseId, BuildSchema(codeType: FieldDbType.Integer));
                for (int i = 1; i <= 4; i++)
                    Insert(dbAccess, "r" + i.ToString(CultureInfo.InvariantCulture), i);
                // Rows 1 and 2 remain; the numbers 3 and 4 have been handed out.
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, "DELETE FROM rebuild_keep_test WHERE sys_no > {0}", 2));

                // Number to text is a change across type families, which every database applies by a rebuild.
                var define = BuildSchema(codeType: FieldDbType.String);
                var plan = Plan(databaseId, define);
                Assert.Equal(UpgradeExecutionMode.Rebuild, plan.Mode);
                Orchestrator().Execute(plan, databaseId);

                Assert.Equal([1L, 2L], Numbers(dbAccess));
                Assert.Equal(["r1", "r2"], Names(dbAccess));

                Insert(dbAccess, "new", 5);
                long next = Numbers(dbAccess).Max();
                // MySQL continues after the highest copied number; the others also skip the numbers a delete freed.
                Assert.True(next > (databaseType == DatabaseType.MySQL ? 2 : 4), $"The next number was {next}.");
                Assert.True(Diff(databaseId, define).IsEmpty);
            });

        private void RunDatabaseOnlyIndex(DatabaseType databaseType)
            => WithTable(databaseType, (databaseId, dbAccess) =>
            {
                Upgrade(databaseId, BuildSchema(codeType: FieldDbType.Integer));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, "CREATE INDEX ix_keep_custom ON rebuild_keep_test (name)"));
                Insert(dbAccess, "kept", 1);

                // Number to text is a change across type families, which every database applies by a rebuild.
                var ex = Assert.Throws<InvalidOperationException>(() => Plan(databaseId, BuildSchema(codeType: FieldDbType.String)));

                Assert.Contains("ix_keep_custom", ex.Message, StringComparison.OrdinalIgnoreCase);
                var real = ReadSchema(databaseId)!;
                Assert.Equal(FieldDbType.Integer, real.Fields!["code"].DbType);
                Assert.Contains(real.Indexes!.Cast<DbTableIndex>(),
                    i => string.Equals(i.Name, "ix_keep_custom", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(["kept"], Names(dbAccess));
            });

        private void RunFailedRebuild(DatabaseType databaseType)
            => WithTable(databaseType, (databaseId, dbAccess) =>
            {
                Upgrade(databaseId, BuildSchema(codeType: FieldDbType.String));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "INSERT INTO rebuild_keep_test (name, code) VALUES ({0}, {1})", "kept", "not a number"));

                var define = BuildSchema(codeType: FieldDbType.Integer);
                Assert.NotNull(Record.Exception(() => Upgrade(databaseId, define)));

                Assert.Equal(FieldDbType.String, ReadSchema(databaseId)!.Fields!["code"].DbType);
                Assert.Equal(["kept"], Names(dbAccess));
                // The next attempt meets the same data and fails the same way; nothing was half done.
                Assert.NotNull(Record.Exception(() => Upgrade(databaseId, define)));
                Assert.Equal(["kept"], Names(dbAccess));
            });

        private static TableSchema BuildSchema(FieldDbType codeType)
        {
            var schema = new TableSchema { TableName = TableName };
            // On SQLite an AutoIncrement column has to be the single-column primary key.
            schema.Fields!.Add("sys_no", "Number", FieldDbType.AutoIncrement);
            schema.Fields!.Add("name", "Name", FieldDbType.String, 20);
            schema.Fields!.Add("code", "Code", codeType, codeType == FieldDbType.String ? 20 : 0);
            schema.Indexes!.AddPrimaryKey("sys_no");
            return schema;
        }

        private static void Insert(DbAccess dbAccess, string name, int code)
        {
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "INSERT INTO rebuild_keep_test (name, code) VALUES ({0}, {1})", name, code));
        }

        private static long[] Numbers(DbAccess dbAccess)
            => Rows(dbAccess, "SELECT sys_no FROM rebuild_keep_test ORDER BY sys_no")
                .Select(r => Convert.ToInt64(r[0], CultureInfo.InvariantCulture)).ToArray();

        private static string[] Names(DbAccess dbAccess)
            => Rows(dbAccess, "SELECT name FROM rebuild_keep_test ORDER BY sys_no")
                .Select(r => Convert.ToString(r[0], CultureInfo.InvariantCulture)!).ToArray();

        private static IEnumerable<DataRow> Rows(DbAccess dbAccess, string sql)
            => dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, sql)).Table!.Rows.Cast<DataRow>();

        private TableUpgradeOrchestrator Orchestrator()
            => new(DbDialectRegistry.Get(_databaseType), _fx.GetRequiredService<IDbConnectionManager>());

        private TableSchema? ReadSchema(string databaseId)
            => DbDialectRegistry.Get(_databaseType)
                .CreateTableSchemaProvider(databaseId, _fx.GetRequiredService<IDbConnectionManager>())
                .GetTableSchema(TableName);

        private TableSchemaDiff Diff(string databaseId, TableSchema define)
            => new TableSchemaComparer(define, ReadSchema(databaseId), _databaseType).CompareToDiff();

        private UpgradePlan Plan(string databaseId, TableSchema define)
            => Orchestrator().Plan(Diff(databaseId, define));

        private void Upgrade(string databaseId, TableSchema define)
            => Orchestrator().Execute(Plan(databaseId, define), databaseId);

        private void WithTable(DatabaseType databaseType, Action<string, DbAccess> test)
        {
            _databaseType = databaseType;
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, databaseType, TableName);
            Drop(dbAccess, databaseType, "tmp_" + TableName);
            try
            {
                test(databaseId, dbAccess);
            }
            finally
            {
                Drop(dbAccess, databaseType, TableName);
                Drop(dbAccess, databaseType, "tmp_" + TableName);
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
