using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Definition.Numbers;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Edge cases of <see cref="FormExpressionCalculator"/>: expressions that do not parse, tables without any
    /// expression, and which seeded values count as empty when defaults fill new rows on save.
    /// </summary>
    public class FormExpressionCalculatorEdgeTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        [Fact]
        [DisplayName("ApplyComputedRow surfaces an expression that does not parse as ExpressionEvaluationException")]
        public void ApplyComputedRow_UnparsableExpression_ThrowsExpressionEvaluationException()
        {
            var schema = new FormSchema("Order", "Order");
            var formTable = schema.Tables!.Add("Order", "Order");
            formTable.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Decimal));
            formTable.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Decimal) { ValueExpression = "qty * (" });
            var row = NewRow(("qty", typeof(decimal), 2m), ("amount", typeof(decimal), 0m));

            Assert.Throws<ExpressionEvaluationException>(
                () => _calculator.ApplyComputedRow(schema, formTable, row, new RoundingContext()));
        }

        [Fact]
        [DisplayName("ApplyDefaultRow leaves out a name the row does not have, so the evaluator reports it as unknown")]
        public void ApplyDefaultRow_UnknownIdentifier_ThrowsExpressionEvaluationException()
        {
            var formTable = new FormSchema("Order", "Order").Tables!.Add("Order", "Order");
            formTable.Fields!.Add(new FormField("note", "Note", FieldDbType.String) { DefaultValueExpression = "missing_field" });
            var row = NewRow(("note", typeof(string), ""));

            Assert.Throws<ExpressionEvaluationException>(() => _calculator.ApplyDefaultRow(formTable, row));
        }

        [Fact]
        [DisplayName("ApplyFieldExpressions leaves a table without any expression untouched")]
        public void ApplyFieldExpressions_TableWithoutExpressions_LeavesRowsUntouched()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add(new FormField("note", "Note", FieldDbType.String));
            var row = NewRow(("note", typeof(string), "kept"));
            var dataSet = new DataSet();
            dataSet.Tables.Add(row.Table);

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal("kept", row["note"]);
            Assert.Equal(DataRowState.Added, row.RowState);
        }

        [Fact]
        [DisplayName("On save a default fills an empty string seeded in a new row, but keeps a non-empty one")]
        public void ApplyFieldExpressions_SeededStrings_OnlyEmptyIsFilled()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add(
                new FormField("status", "Status", FieldDbType.String) { DefaultValueExpression = "\"Draft\"" });
            var table = new DataTable("Order");
            table.Columns.Add("status", typeof(string));
            var empty = table.Rows.Add("");
            var seeded = table.Rows.Add("Open");
            var dataSet = new DataSet();
            dataSet.Tables.Add(table);

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal("Draft", empty["status"]);
            Assert.Equal("Open", seeded["status"]);
        }

        [Fact]
        [DisplayName("On save a default keeps a non-string value already seeded in a new row")]
        public void ApplyFieldExpressions_SeededNumber_IsKept()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add(
                new FormField("qty", "Qty", FieldDbType.Integer) { DefaultValueExpression = "1" });
            var row = NewRow(("qty", typeof(int), 5));
            var dataSet = new DataSet();
            dataSet.Tables.Add(row.Table);

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal(5, row["qty"]);
        }

        [Fact]
        [DisplayName("ApplyNewRowDefaults skips a table with no default expressions and a table missing from the data set")]
        public void ApplyNewRowDefaults_NoDefaultsOrNoDataTable_LeavesRowsUntouched()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add(new FormField("note", "Note", FieldDbType.String));
            schema.Tables!.Add("OrderLine", "Lines").Fields!.Add(
                new FormField("qty", "Qty", FieldDbType.Integer) { DefaultValueExpression = "1" });
            var row = NewRow(("note", typeof(string), "kept"));
            var dataSet = new DataSet();
            dataSet.Tables.Add(row.Table);

            _calculator.ApplyNewRowDefaults(schema, dataSet);

            Assert.Equal("kept", row["note"]);
        }

        private static DataRow NewRow(params (string Name, Type Type, object Value)[] columns)
        {
            var table = new DataTable("Order");
            foreach (var (name, type, _) in columns)
                table.Columns.Add(name, type);
            var row = table.NewRow();
            foreach (var (name, _, value) in columns)
                row[name] = value;
            table.Rows.Add(row);
            return row;
        }
    }
}
