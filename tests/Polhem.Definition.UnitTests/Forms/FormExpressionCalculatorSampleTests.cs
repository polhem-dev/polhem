using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for <see cref="FormExpressionCalculator.EvaluateRuleSample"/> and
    /// <see cref="FormExpressionCalculator.EvaluateFieldSample"/>: one row of sample values gives what the run-time
    /// passes give for the same row.
    /// </summary>
    public class FormExpressionCalculatorSampleTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add(new FormField("price", "Price", FieldDbType.Decimal));
            table.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Decimal));
            table.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Decimal) { ValueExpression = "price * qty" });
            table.Fields!.Add(new FormField("due_date", "Due", FieldDbType.Date) { DefaultValueExpression = "Today().AddDays(30)" });
            table.Fields!.Add(new FormField("note", "Note", FieldDbType.String) { DefaultValueExpression = "null" });
            schema.Rules!.Add(new FormRule("positive_qty", "qty > 0", "Quantity must be positive."));
            return schema;
        }

        private static DataTable BuildTable(object price, object qty)
        {
            var table = new DataTable("Order");
            table.Columns.Add("price", typeof(decimal));
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("due_date", typeof(DateTime));
            table.Columns.Add("note", typeof(string));
            var row = table.NewRow();
            row["price"] = price;
            row["qty"] = qty;
            table.Rows.Add(row);
            return table;
        }

        private static Dictionary<string, object?> Values(object? price, object? qty) =>
            new(StringComparer.Ordinal) { ["price"] = price, ["qty"] = qty };

        [Fact]
        [DisplayName("A ValueExpression sample gives the value ApplyComputedRow writes for the same row")]
        public void EvaluateFieldSample_ValueExpression_MatchesApplyComputedRow()
        {
            var schema = BuildSchema();
            var amount = schema.MasterTable!.Fields!["amount"];
            var table = BuildTable(12.5m, 4m);
            _calculator.ApplyComputedRow(schema, schema.MasterTable!, table.Rows[0], new RoundingContext());

            var sample = _calculator.EvaluateFieldSample(amount, nameof(FormField.ValueExpression), amount.ValueExpression,
                Values("12.5", "4"));

            Assert.Equal(table.Rows[0]["amount"], sample);
        }

        [Fact]
        [DisplayName("A DefaultValueExpression sample gives the value ApplyDefaultRow writes, widened to the field's type")]
        public void EvaluateFieldSample_DefaultValueExpression_MatchesApplyDefaultRow()
        {
            var schema = BuildSchema();
            var dueDate = schema.MasterTable!.Fields!["due_date"];
            var table = BuildTable(0m, 0m);
            _calculator.ApplyDefaultRow(schema.MasterTable!, table.Rows[0], "Asia/Taipei");

            var sample = _calculator.EvaluateFieldSample(dueDate, nameof(FormField.DefaultValueExpression),
                dueDate.DefaultValueExpression, Values(null, null), "Asia/Taipei");

            Assert.IsType<DateTime>(sample);
            Assert.Equal(table.Rows[0]["due_date"], sample);
        }

        [Fact]
        [DisplayName("A default-value expression that yields null leaves the field empty, as ApplyDefaultRow does")]
        public void EvaluateFieldSample_DefaultYieldsNull_ReturnsNull()
        {
            var note = BuildSchema().MasterTable!.Fields!["note"];

            Assert.Null(_calculator.EvaluateFieldSample(note, nameof(FormField.DefaultValueExpression), "null",
                Values(null, null)));
        }

        [Theory]
        [InlineData("3", true)]
        [InlineData("0", false)]
        [InlineData(null, false)]
        [DisplayName("A rule sample is true exactly when ValidateRules lets the same row pass")]
        public void EvaluateRuleSample_MatchesValidateRules(string? qty, bool expected)
        {
            var schema = BuildSchema();
            var dataSet = new DataSet("Order");
            dataSet.Tables.Add(BuildTable(1m, qty is null ? DBNull.Value : decimal.Parse(qty, System.Globalization.CultureInfo.InvariantCulture)));
            var passes = Record.Exception(() => _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave)) is null;

            var sample = _calculator.EvaluateRuleSample("qty > 0", schema.MasterTable!, Values("1", qty));

            Assert.Equal(passes, sample);
            Assert.Equal(expected, sample);
        }

        [Fact]
        [DisplayName("A sample value that cannot be converted to its field's type throws, as a bad cell would")]
        public void EvaluateRuleSample_UnconvertibleValue_Throws()
        {
            var table = BuildSchema().MasterTable!;

            Assert.ThrowsAny<FormatException>(() => _calculator.EvaluateRuleSample("qty > 0", table, Values("1", "abc")));
        }

        [Fact]
        [DisplayName("A field sample for a property that holds no expression is rejected")]
        public void EvaluateFieldSample_OtherProperty_Throws()
        {
            var amount = BuildSchema().MasterTable!.Fields!["amount"];

            Assert.Throws<ArgumentException>(() =>
                _calculator.EvaluateFieldSample(amount, nameof(FormField.Caption), "1", Values(null, null)));
        }

        [Fact]
        [DisplayName("An expression that does not parse throws ExpressionEvaluationException")]
        public void EvaluateRuleSample_ParseError_Throws()
        {
            var table = BuildSchema().MasterTable!;

            Assert.Throws<ExpressionEvaluationException>(() => _calculator.EvaluateRuleSample("qty >", table, Values("1", "1")));
        }
    }
}
