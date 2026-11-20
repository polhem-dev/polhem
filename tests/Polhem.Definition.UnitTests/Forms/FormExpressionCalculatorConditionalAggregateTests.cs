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
    /// Tests for the conditional detail aggregates of <see cref="FormExpressionCalculator"/>:
    /// <c>Count(Table, condition)</c>, <c>Any(Table, condition)</c> and <c>All(Table, condition)</c>, on the save pass,
    /// rules, validation, samples, the client's dependency map and the tables a save completes.
    /// </summary>
    public class FormExpressionCalculatorConditionalAggregateTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildSchema(string countExpression = "Count(OrderLine, qty > 10)")
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            master.Fields!.Add(new FormField("big_lines", "Big lines", FieldDbType.Integer)
            {
                ValueExpression = countExpression,
                ReadOnly = true,
            });
            var detail = schema.Tables!.Add("OrderLine", "Order line");
            detail.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            detail.Fields!.Add(new FormField("product", "Product", FieldDbType.String));
            detail.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Integer));
            detail.Fields!.Add(new FormField("unit_price", "Unit price", FieldDbType.Currency) { NumberKind = NumberKind.UnitPrice });
            return schema;
        }

        // Column names are upper case on purpose: a condition names the declared field names, so it must not depend
        // on the casing the data set stores.
        private static DataSet BuildDataSet(params (string Product, int Qty, decimal Price)[] lines)
        {
            var dataSet = new DataSet();
            var master = dataSet.Tables.Add("Order");
            master.Columns.Add("SYS_ROWID", typeof(Guid));
            master.Columns.Add("BIG_LINES", typeof(int));
            master.Rows.Add(Guid.NewGuid(), 0);

            var detail = dataSet.Tables.Add("OrderLine");
            detail.Columns.Add("SYS_ROWID", typeof(Guid));
            detail.Columns.Add("PRODUCT", typeof(string));
            detail.Columns.Add("QTY", typeof(int));
            detail.Columns.Add("UNIT_PRICE", typeof(decimal));
            foreach (var (product, qty, price) in lines)
                detail.Rows.Add(Guid.NewGuid(), product, qty, price);
            return dataSet;
        }

        private static void AddRule(FormSchema schema, string condition) =>
            schema.Rules!.Add(new FormRule("rule", condition, "Rule failed."));

        [Fact]
        [DisplayName("Count with a condition counts the detail rows the condition holds for")]
        public void ApplyFieldExpressions_ConditionalCount_CountsMatchingRows()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(("A", 5, 1m), ("B", 20, 1m), ("C", 11, 1m));

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal(2, dataSet.Tables["Order"]!.Rows[0]["big_lines"]);
        }

        [Fact]
        [DisplayName("A deleted detail row is not counted by a conditional Count")]
        public void ApplyFieldExpressions_DeletedRow_NotCounted()
        {
            var schema = BuildSchema();
            var dataSet = BuildDataSet(("A", 20, 1m), ("B", 20, 1m));
            dataSet.AcceptChanges();
            dataSet.Tables["OrderLine"]!.Rows[1].Delete();

            _calculator.ApplyFieldExpressions(schema, dataSet, new RoundingContext());

            Assert.Equal(1, dataSet.Tables["Order"]!.Rows[0]["big_lines"]);
        }

        [Theory]
        [DisplayName("All holds when every row meets the condition, and fails when one does not")]
        [InlineData(5, 7, true)]
        [InlineData(5, 0, false)]
        public void ValidateRules_All_EveryRow(int firstQty, int secondQty, bool passes)
        {
            var schema = BuildSchema();
            AddRule(schema, "All(OrderLine, qty > 0)");
            var dataSet = BuildDataSet(("A", firstQty, 1m), ("B", secondQty, 1m));

            var ex = Record.Exception(() => _calculator.ValidateRules(schema, dataSet, FormRuleTrigger.BeforeSave));

            Assert.Equal(passes, ex is null);
        }

        [Fact]
        [DisplayName("Any fails a rule that forbids a zero price on any line, and the condition may call helper functions")]
        public void ValidateRules_NotAny_RejectsMatchingRow()
        {
            var schema = BuildSchema();
            AddRule(schema, "!Any(OrderLine, unit_price == 0 || IsNullOrWhiteSpace(product))");

            _calculator.ValidateRules(schema, BuildDataSet(("A", 1, 3m)), FormRuleTrigger.BeforeSave);
            Assert.Throws<UserMessageException>(() =>
                _calculator.ValidateRules(schema, BuildDataSet(("A", 1, 3m), ("B", 1, 0m)), FormRuleTrigger.BeforeSave));
            Assert.Throws<UserMessageException>(() =>
                _calculator.ValidateRules(schema, BuildDataSet(("  ", 1, 3m)), FormRuleTrigger.BeforeSave));
        }

        [Fact]
        [DisplayName("Over no detail rows, Count is zero, Any is false and All is true")]
        public void ValidateRules_NoRows_EmptyValues()
        {
            var schema = BuildSchema();
            AddRule(schema, "Count(OrderLine, qty > 0) == 0 && !Any(OrderLine, qty > 0) && All(OrderLine, qty > 0)");

            var ex = Record.Exception(() =>
                _calculator.ValidateRules(schema, BuildDataSet(), FormRuleTrigger.BeforeSave));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("A sample evaluates conditional aggregates over no rows")]
        public void EvaluateRuleSample_ConditionalAggregates_EmptyValues()
        {
            var schema = BuildSchema();

            Assert.True(_calculator.EvaluateRuleSample("All(OrderLine, qty > 0) && !Any(OrderLine, qty > 0)",
                schema.MasterTable!, new ExpressionSample { Values = new Dictionary<string, object?>() }));
        }

        [Fact]
        [DisplayName("A master rule sample reads the detail rows it is given through its aggregates")]
        public void EvaluateMasterRuleSample_DetailRows_Aggregated()
        {
            var schema = BuildSchema();
            var lines = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
            {
                ["OrderLine"] =
                [
                    new Dictionary<string, object?> { ["qty"] = 20, ["product"] = "A" },
                    new Dictionary<string, object?> { ["qty"] = 0, ["product"] = "B" },
                ],
            };
            var master = new Dictionary<string, object?>();
            var withLines = new ExpressionSample { Values = master, DetailRows = lines };
            var withNone = new ExpressionSample { Values = master };
            var missing = new ExpressionSample
            {
                Values = master,
                DetailRows = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> { ["Missing"] = [] },
            };

            Assert.True(_calculator.EvaluateMasterRuleSample("Count(OrderLine) == 2 && Count(OrderLine, qty > 10) == 1", schema, withLines));
            Assert.False(_calculator.EvaluateMasterRuleSample("All(OrderLine, qty > 0)", schema, withLines));
            Assert.True(_calculator.EvaluateMasterRuleSample("All(OrderLine, qty > 0) && Count(OrderLine) == 0", schema, withNone));
            Assert.Throws<ArgumentException>(() => _calculator.EvaluateMasterRuleSample("true", schema, missing));
        }

        [Fact]
        [DisplayName("Well-formed conditional aggregates validate without issues")]
        public void ValidateExpressions_ConditionalAggregates_NoIssues()
        {
            var schema = BuildSchema();
            AddRule(schema, "All(OrderLine, qty > 0 && unit_price > 0) && Count(OrderLine, product == \"X\") < 2");

            Assert.Empty(_calculator.ValidateExpressions(schema));
        }

        [Fact]
        [DisplayName("An error in a condition is reported where the condition is written")]
        public void ValidateExpression_ConditionError_PositionInWrittenText()
        {
            var schema = BuildSchema();
            const string expression = "Count(OrderLine) > 0 && All(OrderLine, qty > missing)";

            var ex = Assert.Throws<ExpressionEvaluationException>(() => _calculator.ValidateExpression(
                schema, schema.MasterTable!, nameof(FormRule.Condition), expression));

            Assert.Equal(expression.IndexOf("missing", StringComparison.Ordinal), ex.Position);
        }

        [Theory]
        [DisplayName("A condition that names a master field, is not a bool or holds an aggregate is reported")]
        [InlineData("All(OrderLine, big_lines > 0)")]
        [InlineData("All(OrderLine, qty + 1)")]
        [InlineData("All(OrderLine, Count(OrderLine) > 0)")]
        public void ValidateExpressions_BadCondition_Reported(string condition)
        {
            var schema = BuildSchema();
            AddRule(schema, condition);

            var issue = Assert.Single(_calculator.ValidateExpressions(schema));
            Assert.Equal("rule", issue.RuleId);
        }

        [Fact]
        [DisplayName("The dependency map counts the fields a condition names as read")]
        public void BuildAggregateDependencyMap_ConditionFields_Listed()
        {
            var schema = BuildSchema("Count(OrderLine, qty > 10 && product != \"X\")");

            var fields = Assert.Single(FormExpressionCalculator.BuildAggregateDependencyMap(schema)).Value;

            Assert.Equal(["product", "qty"], fields.Order(StringComparer.Ordinal));
        }

        [Fact]
        [DisplayName("The tables a save completes include those an enabled before-save master rule aggregates")]
        public void GetSaveAggregatedTables_RuleAggregates_Included()
        {
            var schema = BuildSchema();
            schema.MasterTable!.Fields!["big_lines"].ValueExpression = string.Empty;
            Assert.Empty(FormExpressionCalculator.GetSaveAggregatedTables(schema));

            AddRule(schema, "All(OrderLine, qty > 0)");
            Assert.Equal(["OrderLine"], FormExpressionCalculator.GetSaveAggregatedTables(schema));

            schema.Rules!["rule"].Enabled = false;
            Assert.Empty(FormExpressionCalculator.GetSaveAggregatedTables(schema));
        }
    }
}
