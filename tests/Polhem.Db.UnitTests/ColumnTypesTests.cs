using System.ComponentModel;
using System.Data;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Verifies that <see cref="DbCommandSpec.ColumnTypes"/> builds the declared columns before the rows are read, on
    /// both the synchronous and the asynchronous read path.
    /// </summary>
    /// <remarks>
    /// SQLite has no column types, and Microsoft.Data.Sqlite types each result column after its first row. These tests
    /// use a <c>NUMERIC</c> column whose first row is a whole number, the case in which a column typed by the provider
    /// loses the fractional part of every later row.
    /// </remarks>
    public class ColumnTypesTests : IClassFixture<SharedDbFixture>
    {
        private const string DropSql = "DROP TABLE IF EXISTS column_types_test;";
        private const string CreateSql =
            "CREATE TABLE column_types_test (seq INTEGER NOT NULL, flag BOOLEAN NOT NULL, amount NUMERIC(19,4) NOT NULL, note VARCHAR(20) NOT NULL);";
        private const string InsertSql =
            "INSERT INTO column_types_test (seq, flag, amount, note) VALUES ({0}, {1}, {2}, {3});";
        private const string SelectSql = "SELECT seq, flag, amount, note FROM column_types_test ORDER BY seq";

        private readonly SharedDbFixture _fx;
        public ColumnTypesTests(SharedDbFixture fx) { _fx = fx; }

        private DbAccess PrepareTable(bool withRows = true)
        {
            var dbAccess = _fx.NewDbAccess("common_sqlite");
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, CreateSql));
            if (withRows)
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, InsertSql, 1, true, 100m, "a"));
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, InsertSql, 2, false, 100.5m, "b"));
            }
            return dbAccess;
        }

        private static DbCommandSpec TypedSelect()
        {
            var spec = new DbCommandSpec(DbCommandKind.DataTable, SelectSql);
            spec.ColumnTypes["flag"] = typeof(bool);
            spec.ColumnTypes["amount"] = typeof(decimal);
            return spec;
        }

        private void WithTable(Action<DbAccess> body, bool withRows = true)
        {
            var dbAccess = PrepareTable(withRows);
            try
            {
                body(dbAccess);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            }
        }

        private async Task WithTableAsync(Func<DbAccess, Task> body)
        {
            var dbAccess = PrepareTable();
            try
            {
                await body(dbAccess);
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery, DropSql));
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Without ColumnTypes, SQLite types a NUMERIC column by its first row and drops a later fraction")]
        public void ExecuteDataTable_SqliteWithoutColumnTypes_TypesByFirstRow()
            => WithTable(dbAccess =>
            {
                var table = dbAccess.Execute(new DbCommandSpec(DbCommandKind.DataTable, SelectSql)).Table!;

                Assert.Equal(typeof(long), table.Columns["amount"]!.DataType);
                Assert.Equal(100L, table.Rows[1]["amount"]);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes keeps the fraction of a NUMERIC value that follows a whole-number first row")]
        public void ExecuteDataTable_WithColumnTypes_KeepsLaterFractions()
            => WithTable(dbAccess =>
            {
                var table = dbAccess.Execute(TypedSelect()).Table!;

                Assert.Equal(typeof(decimal), table.Columns["amount"]!.DataType);
                Assert.Equal(100m, table.Rows[0]["amount"]);
                Assert.Equal(100.5m, table.Rows[1]["amount"]);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes reads a SQLite BOOLEAN column as bool")]
        public void ExecuteDataTable_WithColumnTypes_ReadsBooleanAsBool()
            => WithTable(dbAccess =>
            {
                var table = dbAccess.Execute(TypedSelect()).Table!;

                Assert.Equal(typeof(bool), table.Columns["flag"]!.DataType);
                Assert.Equal(true, table.Rows[0]["flag"]);
                Assert.Equal(false, table.Rows[1]["flag"]);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes gives the declared types to a result with no rows")]
        public void ExecuteDataTable_WithColumnTypesAndNoRows_TakesDeclaredTypes()
            => WithTable(dbAccess =>
            {
                var table = dbAccess.Execute(TypedSelect()).Table!;

                Assert.Empty(table.Rows);
                Assert.Equal(typeof(bool), table.Columns["flag"]!.DataType);
                Assert.Equal(typeof(decimal), table.Columns["amount"]!.DataType);
            }, withRows: false);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes keeps the columns in result-set order and leaves undeclared columns to the provider")]
        public void ExecuteDataTable_WithColumnTypes_KeepsOrderAndUndeclaredTypes()
            => WithTable(dbAccess =>
            {
                var table = dbAccess.Execute(TypedSelect()).Table!;

                Assert.Equal(["seq", "flag", "amount", "note"], table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                Assert.Equal(typeof(long), table.Columns["seq"]!.DataType);
                Assert.Equal(typeof(string), table.Columns["note"]!.DataType);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes loads the rows as Unchanged")]
        public void ExecuteDataTable_WithColumnTypes_LoadsRowsUnchanged()
            => WithTable(dbAccess =>
            {
                var table = dbAccess.Execute(TypedSelect()).Table!;

                Assert.All(table.Rows.Cast<DataRow>(), row => Assert.Equal(DataRowState.Unchanged, row.RowState));
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes ignores a declared name the result set does not contain")]
        public void ExecuteDataTable_WithColumnTypesForAbsentColumn_IgnoresIt()
            => WithTable(dbAccess =>
            {
                var spec = TypedSelect();
                spec.ColumnTypes["not_selected"] = typeof(int);

                var table = dbAccess.Execute(spec).Table!;

                Assert.Equal(4, table.Columns.Count);
                Assert.False(table.Columns.Contains("not_selected"));
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes matches declared names case-insensitively")]
        public void ExecuteDataTable_WithColumnTypesInOtherCase_MatchesColumn()
            => WithTable(dbAccess =>
            {
                var spec = new DbCommandSpec(DbCommandKind.DataTable, SelectSql);
                spec.ColumnTypes["AMOUNT"] = typeof(decimal);

                var table = dbAccess.Execute(spec).Table!;

                Assert.Equal(100.5m, table.Rows[1]["amount"]);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ExecuteAsync with ColumnTypes keeps the fraction of a NUMERIC value that follows a whole-number first row")]
        public Task ExecuteAsync_WithColumnTypes_KeepsLaterFractions()
            => WithTableAsync(async dbAccess =>
            {
                var table = (await dbAccess.ExecuteAsync(TypedSelect())).Table!;

                Assert.Equal(typeof(decimal), table.Columns["amount"]!.DataType);
                Assert.Equal(100.5m, table.Rows[1]["amount"]);
                Assert.Equal(true, table.Rows[0]["flag"]);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("ColumnTypes on a command that returns no table throws instead of being silently ignored")]
        public void Execute_ColumnTypesOnScalarCommand_Throws()
            => WithTable(dbAccess =>
            {
                var spec = new DbCommandSpec(DbCommandKind.Scalar, "SELECT COUNT(*) FROM column_types_test");
                spec.ColumnTypes["amount"] = typeof(decimal);

                Assert.Throws<InvalidOperationException>(() => dbAccess.Execute(spec));
            });
    }
}
