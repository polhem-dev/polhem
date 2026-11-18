using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Expressions;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for <see cref="FormExpressionCalculator.ValidateExpression"/>: one expression is checked as
    /// <see cref="FormExpressionCalculator.ValidateExpressions"/> checks it, detail aggregates included, and a reported
    /// position points into the expression as written.
    /// </summary>
    public class FormExpressionCalculatorValidateExpressionTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField("total", "Total", FieldDbType.Currency));
            master.Fields!.Add(new FormField("remark", "Remark", FieldDbType.String));
            var detail = schema.Tables!.Add("OrderDetail", "Order detail");
            detail.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Currency));
            return schema;
        }

        private ExpressionEvaluationException? Check(string tableName, string propertyName, string expression)
        {
            var schema = BuildSchema();
            return Record.Exception(() =>
                _calculator.ValidateExpression(schema, schema.Tables![tableName], propertyName, expression))
                as ExpressionEvaluationException;
        }

        [Theory]
        [DisplayName("An aggregate of a detail table is accepted in a master value expression and rule")]
        [InlineData(nameof(FormField.ValueExpression), "Sum(OrderDetail.amount)")]
        [InlineData(nameof(FormRule.Condition), "Count(OrderDetail) > 0")]
        [InlineData(nameof(FormRule.When), "Sum(OrderDetail.amount) > total")]
        public void ValidateExpression_MasterAggregate_Passes(string propertyName, string expression)
        {
            Assert.Null(Check("Order", propertyName, expression));
        }

        [Fact]
        [DisplayName("An error after an aggregate is reported at its position in the expression as written")]
        public void ValidateExpression_ErrorAfterAggregate_PositionInWrittenText()
        {
            const string expression = "Sum(OrderDetail.amount) + missing";

            var ex = Check("Order", nameof(FormField.ValueExpression), expression);

            Assert.NotNull(ex);
            var position = expression.IndexOf("missing", StringComparison.Ordinal);
            Assert.Equal(position, ex.Position);
            Assert.Contains($"(at index {position})", ex.Message, StringComparison.Ordinal);
            Assert.Equal(expression, ex.Expression);
        }

        [Fact]
        [DisplayName("An aggregate naming a table the form does not have is reported at the start of the call")]
        public void ValidateExpression_UnknownAggregateTable_ReportedAtCall()
        {
            const string expression = "total + Sum(Missing.amount)";

            var ex = Check("Order", nameof(FormField.ValueExpression), expression);

            Assert.NotNull(ex);
            Assert.Equal(expression.IndexOf("Sum", StringComparison.Ordinal), ex.Position);
            Assert.Contains("no table 'Missing'", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("An aggregate is rejected in a default value expression and on a detail table")]
        [InlineData("Order", nameof(FormField.DefaultValueExpression))]
        [InlineData("OrderDetail", nameof(FormField.ValueExpression))]
        public void ValidateExpression_AggregateNotAllowed_Throws(string tableName, string propertyName)
        {
            Assert.NotNull(Check(tableName, propertyName, "Count(OrderDetail)"));
        }

        [Fact]
        [DisplayName("A rule expression that is not a bool is rejected, while a field expression may produce any value")]
        public void ValidateExpression_ResultType_FollowsProperty()
        {
            Assert.NotNull(Check("Order", nameof(FormRule.Condition), "total + 1"));
            Assert.Null(Check("Order", nameof(FormField.ValueExpression), "total + 1"));
        }

        [Fact]
        [DisplayName("An empty expression passes")]
        public void ValidateExpression_Empty_Passes()
        {
            Assert.Null(Check("Order", nameof(FormRule.When), string.Empty));
        }

        [Fact]
        [DisplayName("A property name that is not an expression property is rejected")]
        public void ValidateExpression_UnknownProperty_Throws()
        {
            var schema = BuildSchema();

            Assert.Throws<ArgumentException>(() =>
                _calculator.ValidateExpression(schema, schema.MasterTable!, "Caption", "total"));
        }

        [Fact]
        [DisplayName("The single check reports the same message as the whole-form validation")]
        public void ValidateExpression_SameMessageAsValidateExpressions()
        {
            var schema = BuildSchema();
            schema.Rules!.Add(new FormRule("bad", "Sum(OrderDetail.amount) > \"x\"", "Bad."));

            var issue = Assert.Single(_calculator.ValidateExpressions(schema));
            var ex = Assert.Throws<ExpressionEvaluationException>(() =>
                _calculator.ValidateExpression(schema, schema.MasterTable!, nameof(FormRule.Condition), schema.Rules["bad"].Condition));

            Assert.Equal(issue.Message, ex.Message);
        }
    }
}
