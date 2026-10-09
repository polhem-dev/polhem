using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Sorting;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// <c>DataFormRepository</c> reads numeric and boolean columns in the CLR type their FormSchema field declares,
    /// whatever type the provider reports.
    /// </summary>
    /// <remarks>
    /// SQLite types each result column after its first row, so a decimal column whose first row is a whole number
    /// reads as <see cref="long"/> and drops the fractional part of every later row, and a boolean column reads as
    /// <see cref="long"/>. The guard tests run the same check on every provider, so the next declared type a provider
    /// reports differently is caught here rather than by a client decoding the wire.
    /// <para>
    /// One exception is deliberate: on SQLite a Guid column reads back as <see cref="string"/>. Converting it would
    /// round-trip the stored text through <see cref="Guid"/>, and a GUID that SQLite's column default stored without
    /// hyphens would then no longer match on a later write (see the SQLite GUID entry in
    /// <c>maintainers/gotchas/database.md</c>).
    /// </para>
    /// </remarks>
    public class DataFormRepositoryColumnTypeTests : IClassFixture<SharedDbFixture>
    {
        private const string FlagColumn = "is_active";
        private const string ShortColumn = "qty";
        private const string IntegerColumn = "seq";
        private const string LongColumn = "total";
        private const string CurrencyColumn = "price";

        private readonly SharedDbFixture _fx;

        public DataFormRepositoryColumnTypeTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetList keeps the fractional part of a currency value that follows a whole-number first row")]
        public void GetList_SqliteWholeNumberFirstRow_KeepsLaterFractions()
            => WithForm(DatabaseType.SQLite, (form, _) =>
            {
                Insert(form, true, 100m);
                Insert(form, false, 100.5m);

                var table = ReadList(form);

                Assert.Equal(typeof(decimal), table.Columns[CurrencyColumn]!.DataType);
                Assert.Equal(100m, table.Rows[0][CurrencyColumn]);
                Assert.Equal(100.5m, table.Rows[1][CurrencyColumn]);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetList reads a Boolean column as bool rather than as the stored integer")]
        public void GetList_SqliteBooleanColumn_ReadsAsBool()
            => WithForm(DatabaseType.SQLite, (form, _) =>
            {
                Insert(form, true, 1m);
                Insert(form, false, 2m);

                var table = ReadList(form);

                Assert.Equal(typeof(bool), table.Columns[FlagColumn]!.DataType);
                Assert.Equal(true, table.Rows[0][FlagColumn]);
                Assert.Equal(false, table.Rows[1][FlagColumn]);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: in a GetList result with no rows, numeric and boolean columns take their declared types")]
        public void GetList_SqliteEmptyResult_ColumnsTakeDeclaredTypes()
            => WithForm(DatabaseType.SQLite, (form, _) =>
            {
                var table = ReadList(form);

                Assert.Empty(table.Rows);
                AssertDeclaredTypes(form, table);
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: every cell read by GetList and GetData is of its column's declared type")]
        public void Read_Sqlite_EveryCellMatchesDeclaredType() => RunGuard(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: every cell read by GetList and GetData is of its column's declared type")]
        public void Read_SqlServer_EveryCellMatchesDeclaredType() => RunGuard(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: every cell read by GetList and GetData is of its column's declared type")]
        public void Read_PostgreSql_EveryCellMatchesDeclaredType() => RunGuard(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: every cell read by GetList and GetData is of its column's declared type")]
        public void Read_MySql_EveryCellMatchesDeclaredType() => RunGuard(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: every cell read by GetList and GetData is of its column's declared type")]
        public void Read_Oracle_EveryCellMatchesDeclaredType() => RunGuard(DatabaseType.Oracle);

        private void RunGuard(DatabaseType databaseType)
            => WithForm(databaseType, (form, progId) =>
            {
                Insert(form, true, 100m);
                var rowId = Insert(form, false, 100.5m);

                var list = ReadList(form);
                AssertDeclaredTypes(form, list);
                AssertCellsMatchColumns(list);
                Assert.Equal(100.5m, list.Rows[1][CurrencyColumn]);

                var data = form.Repository.GetData(rowId)!.Tables[progId]!;
                AssertDeclaredTypes(form, data);
                AssertCellsMatchColumns(data);
            });

        private static DataTable ReadList(TransientForm form)
            => form.Repository.GetList(string.Empty, null, [new SortField(SysFields.No, SortDirection.Asc)]).Table!;

        private static void AssertDeclaredTypes(TransientForm form, DataTable table)
        {
            foreach (FormField field in form.Schema.MasterTable!.Fields!)
            {
                var column = table.Columns[field.FieldName];
                Assert.NotNull(column);
                var expected = form.DatabaseType == DatabaseType.SQLite && field.DbType == FieldDbType.Guid
                    ? typeof(string)
                    : DbTypeConverter.ToType(field.DbType);
                Assert.Equal(expected, column.DataType);
            }
        }

        private static void AssertCellsMatchColumns(DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                foreach (DataColumn column in table.Columns)
                {
                    var value = row[column];
                    if (value is DBNull) { continue; }
                    Assert.True(value.GetType() == column.DataType,
                        $"Column '{column.ColumnName}' is {column.DataType.Name} but holds {value.GetType().Name}.");
                }
            }
        }

        private void WithForm(DatabaseType databaseType, Action<TransientForm, string> body)
        {
            string progId = TransientForm.NewTableName("tb_cty_");
            var schema = new FormSchema(progId, "Column types") { CategoryId = TransientForm.CategoryId };
            var table = schema.Tables!.Add(progId, "Column types");
            table.Fields!.Add(SysFields.No, "No", FieldDbType.AutoIncrement);
            table.Fields.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields.Add(FlagColumn, "Active", FieldDbType.Boolean);
            table.Fields.Add(ShortColumn, "Quantity", FieldDbType.Short);
            table.Fields.Add(IntegerColumn, "Sequence", FieldDbType.Integer);
            table.Fields.Add(LongColumn, "Total", FieldDbType.Long);
            table.Fields.Add(CurrencyColumn, "Price", FieldDbType.Currency);

            var form = new TransientForm(_fx, databaseType, schema);
            form.CreateTables();
            try
            {
                body(form, progId);
            }
            finally
            {
                form.DropTables();
            }
        }

        private static Guid Insert(TransientForm form, bool flag, decimal price)
        {
            var rowId = Guid.NewGuid();
            var progId = form.Schema.ProgId;
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(progId)} ({form.Quote(SysFields.RowId)}, {form.Quote(FlagColumn)}, " +
                $"{form.Quote(ShortColumn)}, {form.Quote(IntegerColumn)}, {form.Quote(LongColumn)}, {form.Quote(CurrencyColumn)}) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                rowId, flag, (short)2, 3, 4L, price);
            return rowId;
        }
    }
}
