using System.ComponentModel;
using System.Globalization;
using Polhem.Db.Manager;
using Polhem.Core.Data;
using Polhem.Db.Schema;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// The database default of a <see cref="FieldDbType.Date"/> column must store midnight of the current UTC day, so a
    /// row that takes it matches a filter on its own day. Both paths that write the default are covered: the backfill
    /// when an upgrade adds the column to a table that has rows, and an INSERT that omits the column.
    /// </summary>
    public class DateColumnDefaultDbTests : IClassFixture<SharedDbFixture>
    {
        private const string TableName = "date_default_test";

        private readonly SharedDbFixture _fx;

        public DateColumnDefaultDbTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a defaulted Date column stores the current UTC day and the schema reads back unchanged")]
        public void DateDefault_Sqlite_MatchesTodayAndReadsBackUnchanged() => Run(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a defaulted Date column stores the current UTC day and the schema reads back unchanged")]
        public void DateDefault_SqlServer_MatchesTodayAndReadsBackUnchanged() => Run(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a defaulted Date column stores the current UTC day and the schema reads back unchanged")]
        public void DateDefault_PostgreSql_MatchesTodayAndReadsBackUnchanged() => Run(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a defaulted Date column stores the current UTC day and the schema reads back unchanged")]
        public void DateDefault_MySql_MatchesTodayAndReadsBackUnchanged() => Run(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a defaulted Date column stores the current UTC day and the schema reads back unchanged")]
        public void DateDefault_Oracle_MatchesTodayAndReadsBackUnchanged() => Run(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a Date column that still carries the old CURRENT_TIMESTAMP default reads back unchanged")]
        public void LegacyDateDefault_Sqlite_ReadsBackUnchanged() => RunLegacy(DatabaseType.SQLite, "CURRENT_TIMESTAMP");

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a Date column that still carries the old SYS_EXTRACT_UTC(SYSTIMESTAMP) default reads back unchanged")]
        public void LegacyDateDefault_Oracle_ReadsBackUnchanged() => RunLegacy(DatabaseType.Oracle, "SYS_EXTRACT_UTC(SYSTIMESTAMP)");

        /// <summary>
        /// A database created before the Date default kept only the day must not plan a change for it: the change
        /// would rebuild the table on SQLite, which an upgrade cannot do when the same plan renames a column.
        /// </summary>
        private void RunLegacy(DatabaseType databaseType, string legacyDefault)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, databaseType);
            try
            {
                var define = BuildSchema(withDate: true);
                var dialect = DbDialectRegistry.Get(databaseType);
                string ddl = dialect.CreateCreateTableCommandBuilder().GetCommandText(define)
                    .Replace(dialect.GetDefaultValueExpression(FieldDbType.Date), legacyDefault, StringComparison.Ordinal);
                Assert.Contains(legacyDefault, ddl, StringComparison.Ordinal);
                // The upgrade runs a create script as one command too.
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, ddl));

                Assert.True(Diff(databaseId, define).IsEmpty);
            }
            finally
            {
                Drop(dbAccess, databaseType);
            }
        }

        private void Run(DatabaseType databaseType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, databaseType);
            try
            {
                var before = DateOnly.FromDateTime(DateTime.UtcNow);

                Upgrade(databaseId, BuildSchema(withDate: false));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "INSERT INTO date_default_test (id) VALUES ({0})", "1"));
                // The upgrade backfills the existing row from the column's default.
                Upgrade(databaseId, BuildSchema(withDate: true));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "INSERT INTO date_default_test (id) VALUES ({0})", "2"));

                var after = DateOnly.FromDateTime(DateTime.UtcNow);

                // The two reads differ only when the test runs across midnight UTC; either day is then correct.
                int matched = CountOn(dbAccess, before);
                if (matched != 2 && after != before)
                    matched = CountOn(dbAccess, after);
                Assert.Equal(2, matched);

                Assert.True(Diff(databaseId, BuildSchema(withDate: true)).IsEmpty);
            }
            finally
            {
                Drop(dbAccess, databaseType);
            }
        }

        private static TableSchema BuildSchema(bool withDate)
        {
            var schema = new TableSchema { TableName = TableName };
            // A String key: SQLite reads an INTEGER primary key back as AutoIncrement, which would be a difference of its own.
            schema.Fields!.Add("id", "ID", FieldDbType.String, 10);
            if (withDate)
                schema.Fields!.Add("order_date", "Order date", FieldDbType.Date);
            schema.Indexes!.AddPrimaryKey("id");
            return schema;
        }

        private TableSchemaDiff Diff(string databaseId, TableSchema define)
        {
            var connectionManager = _fx.GetRequiredService<IDbConnectionManager>();
            var databaseType = connectionManager.GetConnectionInfo(databaseId).DatabaseType;
            var real = DbDialectRegistry.Get(databaseType)
                .CreateTableSchemaProvider(databaseId, connectionManager)
                .GetTableSchema(TableName);
            return new TableSchemaComparer(define, real, databaseType).CompareToDiff();
        }

        private void Upgrade(string databaseId, TableSchema define)
        {
            var orchestrator = new TableUpgradeOrchestrator(databaseId, _fx.GetRequiredService<IDbConnectionManager>());
            orchestrator.Execute(orchestrator.Plan(Diff(databaseId, define)), databaseId);
        }

        private static int CountOn(DbAccess dbAccess, DateOnly day)
        {
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM date_default_test WHERE order_date = {0}", day));
            return Convert.ToInt32(result.Scalar!, CultureInfo.InvariantCulture);
        }

        private static void Drop(DbAccess dbAccess, DatabaseType databaseType)
        {
            // Oracle has no DROP TABLE IF EXISTS, so a PL/SQL block swallows ORA-00942 instead.
            string sql = databaseType switch
            {
                DatabaseType.SQLServer => "IF OBJECT_ID(N'date_default_test', N'U') IS NOT NULL DROP TABLE date_default_test;",
                DatabaseType.Oracle =>
                    "BEGIN EXECUTE IMMEDIATE 'DROP TABLE DATE_DEFAULT_TEST CASCADE CONSTRAINTS'; " +
                    "EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; END;",
                _ => "DROP TABLE IF EXISTS date_default_test",
            };
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, sql));
        }
    }
}
