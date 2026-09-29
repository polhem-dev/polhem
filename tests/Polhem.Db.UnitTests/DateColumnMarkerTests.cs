using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Verifies that <see cref="DbCommandSpec.DateColumns"/> (path two: SQL written by the caller) marks calendar
    /// date columns on the <c>DbAccess</c> read path. ADO.NET always reports a date column as <c>System.DateTime</c>,
    /// so the definition layer's Date / DateTime distinction disappears on the SQL read path; this declaration is how
    /// the caller restores it.
    /// </summary>
    public class DateColumnMarkerTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public DateColumnMarkerTests(SharedDbFixture fx) { _fx = fx; }

        private const string DropSql = "DROP TABLE IF EXISTS date_marker_test;";
        private const string CreateSql =
            "CREATE TABLE date_marker_test (order_date DATE NOT NULL, created_at DATETIME NOT NULL);";
        private const string InsertSql =
            "INSERT INTO date_marker_test (order_date, created_at) VALUES ({0}, {1});";

        private DbAccess PrepareTable()
        {
            var dbAccess = _fx.NewDbAccess("common_sqlite");
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, CreateSql));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, InsertSql,
                new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Unspecified),
                new DateTime(2026, 7, 25, 13, 45, 0, DateTimeKind.Unspecified)));
            return dbAccess;
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Date columns carry no marker when DateColumns is not declared (existing behavior unchanged)")]
        public void ExecuteDataTable_WithoutDeclaration_LeavesColumnsUnmarked()
        {
            var dbAccess = PrepareTable();
            try
            {
                var spec = new DbCommandSpec(DbCommandKind.DataTable,
                    "SELECT order_date, created_at FROM date_marker_test");
                var table = dbAccess.Execute(spec).Table!;

                Assert.Null(table.Columns["order_date"]!.GetDeclaredFieldDbType());
                Assert.Null(table.Columns["created_at"]!.GetDeclaredFieldDbType());
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Declaring DateColumns marks only the listed columns as Date")]
        public void ExecuteDataTable_WithDeclaration_MarksOnlyDeclaredColumns()
        {
            var dbAccess = PrepareTable();
            try
            {
                var spec = new DbCommandSpec(DbCommandKind.DataTable,
                    "SELECT order_date, created_at FROM date_marker_test");
                spec.DateColumns.Add("order_date");
                var table = dbAccess.Execute(spec).Table!;

                Assert.Equal(FieldDbType.Date, table.Columns["order_date"]!.ResolveFieldDbType());
                Assert.Null(table.Columns["created_at"]!.GetDeclaredFieldDbType());
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("DateColumns naming a column that does not exist throws instead of being silently ignored")]
        public void ExecuteDataTable_UnknownDeclaredColumn_Throws()
        {
            var dbAccess = PrepareTable();
            try
            {
                var spec = new DbCommandSpec(DbCommandKind.DataTable,
                    "SELECT order_date FROM date_marker_test");
                spec.DateColumns.Add("oder_date");

                Assert.Throws<ArgumentException>(() => dbAccess.Execute(spec));
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("DateColumns on a DbCommandKind other than DataTable throws")]
        public void Execute_DateColumnsOnNonTableKind_Throws()
        {
            var dbAccess = PrepareTable();
            try
            {
                var spec = new DbCommandSpec(DbCommandKind.Scalar,
                    "SELECT COUNT(*) FROM date_marker_test");
                spec.DateColumns.Add("order_date");

                // A declaration that silently has no effect is exactly the failure mode this mechanism removes,
                // so it throws instead of being ignored.
                Assert.Throws<InvalidOperationException>(() => dbAccess.Execute(spec));
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("The asynchronous read path applies the DateColumns declaration too")]
        public async Task ExecuteAsync_WithDeclaration_MarksDeclaredColumns()
        {
            var dbAccess = PrepareTable();
            try
            {
                var spec = new DbCommandSpec(DbCommandKind.DataTable,
                    "SELECT order_date, created_at FROM date_marker_test");
                spec.DateColumns.Add("order_date");
                var result = await dbAccess.ExecuteAsync(spec);

                Assert.Equal(FieldDbType.Date, result.Table!.Columns["order_date"]!.ResolveFieldDbType());
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            }
        }
    }
}
