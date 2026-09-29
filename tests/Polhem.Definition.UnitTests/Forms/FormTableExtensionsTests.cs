using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Verifies <see cref="FormTableExtensions.ApplyFieldDbTypes"/> (path one: the framework tags columns from the schema).
    /// ADO.NET reports every date column as System.DateTime, so a DataTable read back from SQL must have the schema's
    /// field types replayed onto it to match the shape of an empty DataTable built from the schema.
    /// </summary>
    public class FormTableExtensionsTests
    {
        private static FormTable BuildFormTable()
        {
            var schema = new FormSchema("Order", "Order");
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("order_date", "Order Date", FieldDbType.Date);
            table.Fields.Add("created_at", "Created At", FieldDbType.DateTime);
            table.Fields.Add("amount", "Amount", FieldDbType.Currency);
            return table;
        }

        private static DataTable BuildProviderTable()
        {
            // Mimic the types ADO.NET reports: Date and DateTime are both DateTime, and Currency is decimal.
            var table = new DataTable("Order");
            table.Columns.Add("order_date", typeof(DateTime));
            table.Columns.Add("created_at", typeof(DateTime));
            table.Columns.Add("amount", typeof(decimal));
            return table;
        }

        [Fact]
        [DisplayName("ApplyFieldDbTypes replays the schema's field types onto the columns read back from SQL")]
        public void ApplyFieldDbTypes_MarksColumnsFromSchema()
        {
            var table = BuildProviderTable();

            BuildFormTable().ApplyFieldDbTypes(table);

            Assert.Equal(FieldDbType.Date, table.Columns["order_date"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.DateTime, table.Columns["created_at"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.Currency, table.Columns["amount"]!.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("ApplyFieldDbTypes does not change the CLR type of a column")]
        public void ApplyFieldDbTypes_LeavesClrTypesUnchanged()
        {
            var table = BuildProviderTable();

            BuildFormTable().ApplyFieldDbTypes(table);

            Assert.Equal(typeof(DateTime), table.Columns["order_date"]!.DataType);
            Assert.Equal(typeof(DateTime), table.Columns["created_at"]!.DataType);
        }

        [Fact]
        [DisplayName("A column the schema does not cover stays untagged without throwing")]
        public void ApplyFieldDbTypes_ColumnsOutsideSchema_LeftUnmarked()
        {
            // A query may return columns beyond the declared fields (aggregates, expressions), and those still infer from the CLR type.
            var table = BuildProviderTable();
            table.Columns.Add("row_count", typeof(int));

            BuildFormTable().ApplyFieldDbTypes(table);

            Assert.Null(table.Columns["row_count"]!.GetDeclaredFieldDbType());
            Assert.Equal(FieldDbType.Integer, table.Columns["row_count"]!.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("A field the schema declares but the query does not return does not throw")]
        public void ApplyFieldDbTypes_FieldsMissingFromResult_DoNotThrow()
        {
            // Partial-column queries (such as selecting only `sys_rowid`) are routine and must not fail.
            var table = new DataTable("Order");
            table.Columns.Add("order_date", typeof(DateTime));

            var exception = Record.Exception(() => BuildFormTable().ApplyFieldDbTypes(table));

            Assert.Null(exception);
            Assert.Equal(FieldDbType.Date, table.Columns["order_date"]!.ResolveFieldDbType());
        }
    }
}
