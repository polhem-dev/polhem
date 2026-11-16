using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Definition.Numbers;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for the detail aggregates of <see cref="FormExpressionCalculator"/>: a master field or rule that reads
    /// <c>Sum</c>, <c>Count</c>, <c>Min</c> or <c>Max</c> of a detail table, on the save pass, the client's row pass,
    /// validation and samples.
    /// </summary>
    public class FormExpressionCalculatorAggregateTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildSchema(string totalExpression = "Sum(RequestItem.line_amount)")
        {
            var schema = new FormSchema("Request", "Request") { CategoryId = "company" };
            var master = schema.Tables!.Add("Request", "Request");
            master.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            master.Fields!.Add(new FormField("total", "Total", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = totalExpression,
                ReadOnly = true,
            });
            master.Fields!.Add(new FormField("item_count", "Items", FieldDbType.Integer)
            {
                ValueExpression = "Count(RequestItem)",
                ReadOnly = true,
            });
            master.Fields!.Add(new FormField("first_needed", "First needed", FieldDbType.Date)
            {
                ValueExpression = "Min(RequestItem.needed_by)",
                ReadOnly = true,
            });

            var detail = schema.Tables!.Add("RequestItem", "Request item");
            detail.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            detail.Fields!.Add(new FormField("description", "Description", FieldDbType.String));
            detail.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Integer));
            detail.Fields!.Add(new FormField("unit_price", "Unit price", FieldDbType.Currency) { NumberKind = NumberKind.UnitPrice });
            detail.Fields!.Add(new FormField("line_amount", "Amount", FieldDbType.Currency)
            {
                NumberKind = NumberKind.Amount,
                ValueExpression = "qty * unit_price",
                ReadOnly = true,
            });
            detail.Fields!.Add(new FormField("needed_by", "Needed by", FieldDbType.Date));
            return schema;
        }

        // Column names are upper case on purpose: variables are keyed by the declared field names, so an aggregate
        // must not depend on the casing the data set stores.
        private static DataSet BuildDataSet(params (int Qty, decimal Price, DateTime NeededBy)[] items)
        {
            var dataSet = new DataSet();
            var master = dataSet.Tables.Add("Request");
            master.Columns.Add("SYS_ROWID", typeof(Guid));
            master.Columns.Add("TOTAL", typeof(decimal));
            master.Columns.Add("ITEM_COUNT", typeof(int));
            master.Columns.Add("FIRST_NEEDED", typeof(DateTime));
            master.Rows.Add(Guid.NewGuid(), 0m, 0, DBNull.Value);

            var detail = dataSet.Tables.Add("RequestItem");
            detail.Columns.Add("SYS_ROWID", typeof(Guid));
            detail.Columns.Add("DESCRIPTION", typeof(string));
            detail.Columns.Add("QTY", typeof(int));
            detail.Columns.Add("UNIT_PRICE", typeof(decimal));
            detail.Columns.Add("LINE_AMOUNT", typeof(decimal));
            detail.Columns.Add("NEEDED_BY", typeof(DateTime));
            foreach (var (qty, price, neededBy) in items)
                detail.Rows.Add(Guid.NewGuid(), "item", qty, price, 0m, neededBy);
            return dataSet;
        }

        private static readonly DateTime s_june1 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime s_may20 = new(2026, 5, 20, 0, 0, 0, DateTimeKind.Unspecified);

        [Fact]
        [DisplayName("The save pass computes the detail lines first, then the master aggregates")]
        public void ApplyFieldExpressions_NewRecord_MasterAggregatesComputedDetailLines()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet((2, 10m, s_june1), (3, 15m, s_may20));

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            var master = dataSet.Tables["Request"]!.Rows[0];
            Assert.Equal(65m, master["total"]);
            Assert.Equal(2, master["item_count"]);
            Assert.Equal(s_may20, master["first_needed"]);
        }

        [Fact]
        [DisplayName("Each detail value is rounded to its field's decimals before the sum")]
        public void ApplyFieldExpressions_Sum_RoundsEachRowBeforeSumming()
        {
            var schema = BuildSchema("Sum(RequestItem.unit_price)");
            schema.Tables!["RequestItem"].Fields!["unit_price"].NumberKind = NumberKind.Amount;
            var dataSet = BuildDataSet((1, 0.005m, s_june1), (1, 0.005m, s_june1), (1, 0.005m, s_june1));

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            // Rounded per row: 0.01 * 3. Summing the full precision first would give round(0.015) = 0.02.
            Assert.Equal(0.03m, dataSet.Tables["Request"]!.Rows[0]["total"]);
        }

        [Fact]
        [DisplayName("A deleted detail row is not aggregated")]
        public void ApplyFieldExpressions_DeletedDetailRow_Excluded()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet((2, 10m, s_june1), (3, 15m, s_may20));
            dataSet.AcceptChanges();
            dataSet.Tables["RequestItem"]!.Rows[1].Delete();

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            var master = dataSet.Tables["Request"]!.Rows[0];
            Assert.Equal(1, master["item_count"]);
        }

        [Fact]
        [DisplayName("An unchanged master is recomputed and becomes modified when an aggregated detail row changes")]
        public void ApplyFieldExpressions_DetailModified_UnchangedMasterRecomputed()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet((2, 10m, s_june1));
            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());
            dataSet.AcceptChanges();

            dataSet.Tables["RequestItem"]!.Rows[0]["qty"] = 5;
            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            var master = dataSet.Tables["Request"]!.Rows[0];
            Assert.Equal(DataRowState.Modified, master.RowState);
            Assert.Equal(50m, master["total"]);
        }

        [Fact]
        [DisplayName("An unchanged master stays unchanged when no detail row changed")]
        public void ApplyFieldExpressions_NothingChanged_MasterStaysUnchanged()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet((2, 10m, s_june1));
            dataSet.AcceptChanges();

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal(DataRowState.Unchanged, dataSet.Tables["Request"]!.Rows[0].RowState);
        }

        [Fact]
        [DisplayName("A master aggregate can be part of a larger expression")]
        public void ApplyComputedRow_CombinedExpression_UsesAggregate()
        {
            var schema = BuildSchema("Sum(RequestItem.line_amount) * 1.05m");
            var dataSet = BuildDataSet((2, 10m, s_june1));
            dataSet.Tables["RequestItem"]!.Rows[0]["line_amount"] = 20m;
            var master = dataSet.Tables["Request"]!.Rows[0];

            var changed = _calculator.ApplyComputedRow(schema, schema.MasterTable!, master, new RoundingContext());

            Assert.Contains("total", changed);
            Assert.Equal(21m, master["total"]);
        }

        [Fact]
        [DisplayName("Aggregates over no detail rows are zero or the field's empty value")]
        public void ApplyComputedRow_NoDetailRows_ZeroAndEmpty()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet();
            var master = dataSet.Tables["Request"]!.Rows[0];
            master["total"] = 99m;

            _calculator.ApplyComputedRow(schema, schema.MasterTable!, master, new RoundingContext());

            Assert.Equal(0m, master["total"]);
            Assert.Equal(0, master["item_count"]);
        }

        [Fact]
        [DisplayName("An aggregate in a detail table's expression fails to evaluate")]
        public void ApplyComputedRow_AggregateOnDetail_Throws()
        {
            var schema = BuildSchema();
            schema.Tables!["RequestItem"].Fields!["line_amount"].ValueExpression = "Sum(RequestItem.qty)";
            var dataSet = BuildDataSet((2, 10m, s_june1));

            Assert.Throws<ExpressionEvaluationException>(() => _calculator.ApplyComputedRow(
                schema, schema.Tables!["RequestItem"], dataSet.Tables["RequestItem"]!.Rows[0], new RoundingContext()));
        }

        [Fact]
        [DisplayName("A master rule can require at least one detail row")]
        public void ValidateRules_CountRule_FailsWithoutDetailRows()
        {
            var schema = BuildSchema();
            schema.Rules!.Add(new FormRule("has_items", "Count(RequestItem) > 0", "At least one item is required."));

            var empty = BuildDataSet();
            var ex = Assert.Throws<UserMessageException>(() =>
                _calculator.ValidateRules(schema, empty, FormRuleTrigger.BeforeSave));
            Assert.Contains("At least one item", ex.Message);

            _calculator.ValidateRules(schema, BuildDataSet((1, 1m, s_june1)), FormRuleTrigger.BeforeSave);
        }

        [Fact]
        [DisplayName("The dependency map lists each aggregated detail table and its fields")]
        public void BuildAggregateDependencyMap_ListsTablesAndFields()
        {
            var map = FormExpressionCalculator.BuildAggregateDependencyMap(BuildSchema());

            var fields = Assert.Single(map).Value;
            Assert.True(map.ContainsKey("requestitem"));
            Assert.Equal(["line_amount", "needed_by"], fields.Order(StringComparer.Ordinal));
        }

        [Fact]
        [DisplayName("A schema whose aggregates are well formed validates without issues")]
        public void ValidateExpressions_ValidAggregates_NoIssues()
        {
            var schema = BuildSchema();
            schema.Rules!.Add(new FormRule("has_items", "Count(RequestItem) > 0", "Required."));

            Assert.Empty(_calculator.ValidateExpressions(schema));
        }

        [Theory]
        [DisplayName("A malformed aggregate is reported")]
        [InlineData("Sum(Missing.amount)", "no table 'Missing'")]
        [InlineData("Sum(RequestItem.missing)", "no field 'missing'")]
        [InlineData("Sum(RequestItem.description)", "cannot aggregate")]
        [InlineData("Sum(Request.total)", "is the master table")]
        public void ValidateExpressions_MalformedAggregate_Reported(string expression, string expected)
        {
            var schema = BuildSchema(expression);

            var issue = Assert.Single(_calculator.ValidateExpressions(schema));
            Assert.Equal("total", issue.FieldName);
            Assert.Contains(expected, issue.Message);
        }

        [Fact]
        [DisplayName("An aggregate is reported outside the master table and in a default value expression")]
        public void ValidateExpressions_AggregateOutsideMasterValue_Reported()
        {
            var schema = BuildSchema();
            schema.Tables!["RequestItem"].Fields!["line_amount"].ValueExpression = "Sum(RequestItem.qty)";
            schema.MasterTable!.Fields!["sys_rowid"].DefaultValueExpression = "Count(RequestItem)";
            schema.Rules!.Add(new FormRule("detail_rule", "Count(RequestItem) > 0", "x") { TargetTable = "RequestItem" });

            var issues = _calculator.ValidateExpressions(schema);

            Assert.Equal(3, issues.Count);
            Assert.Contains(issues, i => i.FieldName == "line_amount" && i.Message.Contains("detail table"));
            Assert.Contains(issues, i => i.PropertyName == nameof(FormField.DefaultValueExpression));
            Assert.Contains(issues, i => i.RuleId == "detail_rule");
        }

        [Fact]
        [DisplayName("A sample evaluates an aggregate over no rows")]
        public void EvaluateFieldSample_Aggregate_ZeroOverNoRows()
        {
            var schema = BuildSchema("Sum(RequestItem.line_amount) + 5m");
            var field = schema.MasterTable!.Fields!["total"];

            var value = _calculator.EvaluateFieldSample(field, nameof(FormField.ValueExpression), field.ValueExpression,
                new Dictionary<string, object?>());

            Assert.Equal(5m, value);
        }
    }
}
