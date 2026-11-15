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
        [DisplayName("SQLite: an upgrade moves a Date column off the old CURRENT_TIMESTAMP default and keeps the rows")]
        public void LegacyDateDefault_Sqlite_UpgradeRefreshesDefaultAndKeepsRows()
            => RunLegacy(DatabaseType.SQLite, "CURRENT_TIMESTAMP", tableRebuilt: true);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: an upgrade moves a Date column off the old SYS_EXTRACT_UTC(SYSTIMESTAMP) default and keeps the rows")]
        public void LegacyDateDefault_Oracle_UpgradeRefreshesDefaultAndKeepsRows()
            => RunLegacy(DatabaseType.Oracle, "SYS_EXTRACT_UTC(SYSTIMESTAMP)", tableRebuilt: false);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: an upgrade that renames a column and rebuilds the table copies the column into its new name")]
        public void RenameWithRebuild_Sqlite_CopiesRenamedColumn()
        {
            var databaseId = TestDbConventions.GetDatabaseId(DatabaseType.SQLite);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, DatabaseType.SQLite);
            try
            {
                var before = BuildSchema(withDate: false);
                before.Fields!.Add("name", "Name", FieldDbType.String, 20);
                Upgrade(databaseId, before);
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "INSERT INTO date_default_test (id, name) VALUES ({0}, {1})", "1", "kept"));

                // Adding a NOT NULL Date column to a table with rows needs a rebuild on SQLite.
                var after = BuildSchema(withDate: true);
                after.Fields!.Add("title", "Title", FieldDbType.String, 20);
                after.Fields!["title"].OriginalFieldName = "name";
                Upgrade(databaseId, after);

                var title = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                    "SELECT title FROM date_default_test WHERE id = {0}", "1")).Scalar;
                Assert.Equal("kept", Convert.ToString(title, CultureInfo.InvariantCulture));
                Assert.True(Diff(databaseId, after).IsEmpty);
            }
            finally
            {
                Drop(dbAccess, DatabaseType.SQLite);
            }
        }

        /// <summary>
        /// A database created with an earlier built-in Date default reads back different from the definition, so
        /// an upgrade changes the column to the current default and keeps its rows. The table also carries a Date
        /// column the definition does not declare: a rebuild (SQLite) gives it the current default as well, while an
        /// in-place change of the declared column (Oracle) leaves it alone.
        /// </summary>
        private void RunLegacy(DatabaseType databaseType, string legacyDefault, bool tableRebuilt)
        {
            var databaseId = TestDbConventions.GetDatabaseId(databaseType);
            var dbAccess = _fx.NewDbAccess(databaseId);
            Drop(dbAccess, databaseType);
            try
            {
                var define = BuildSchema(withDate: true);
                var created = BuildSchema(withDate: true);
                created.Fields!.Add("extra_date", "Extra date", FieldDbType.Date);
                var dialect = DbDialectRegistry.Get(databaseType);
                string ddl = dialect.CreateCreateTableCommandBuilder().GetCommandText(created)
                    .Replace(dialect.GetDefaultValueExpression(FieldDbType.Date), legacyDefault, StringComparison.Ordinal);
                Assert.Contains(legacyDefault, ddl, StringComparison.Ordinal);
                // The upgrade runs a create script as one command too.
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, ddl));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "INSERT INTO date_default_test (id, order_date, extra_date) VALUES ({0}, {1}, {2})",
                    "1", new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3)));

                Assert.False(Diff(databaseId, define).IsEmpty);
                var before = DateOnly.FromDateTime(DateTime.UtcNow);
                Upgrade(databaseId, define);
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    "INSERT INTO date_default_test (id) VALUES ({0})", "2"));
                var after = DateOnly.FromDateTime(DateTime.UtcNow);

                Assert.True(Diff(databaseId, define).IsEmpty);
                Assert.Equal(1, CountWhere(dbAccess, "id = {0} AND order_date = {1}", "1", new DateOnly(2026, 1, 2)));
                Assert.Equal(1, CountWhere(dbAccess, "id = {0} AND extra_date = {1}", "1", new DateOnly(2026, 1, 3)));
                Assert.True(OnDay(dbAccess, "order_date", before, after));
                if (tableRebuilt)
                    Assert.True(OnDay(dbAccess, "extra_date", before, after));
            }
            finally
            {
                Drop(dbAccess, databaseType);
            }
        }

        // The two days differ only when the test runs across midnight UTC; either day is then correct.
        private static bool OnDay(DbAccess dbAccess, string column, DateOnly before, DateOnly after)
            => CountWhere(dbAccess, $"id = {{0}} AND {column} = {{1}}", "2", before) == 1
                || (after != before && CountWhere(dbAccess, $"id = {{0}} AND {column} = {{1}}", "2", after) == 1);

        private static int CountWhere(DbAccess dbAccess, string condition, params object[] values)
        {
            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                "SELECT COUNT(*) FROM date_default_test WHERE " + condition, values));
            return Convert.ToInt32(result.Scalar!, CultureInfo.InvariantCulture);
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
