using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Expressions;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Expressions;
using Polhem.Tests.Shared;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for the allowlist checks of <see cref="FormExpressionCalculator"/>: the whole-schema and single-expression
    /// findings, with detail aggregates and their conditions placed where they are written, and the strict check for an
    /// expression from an untrusted author.
    /// </summary>
    public class FormExpressionCalculatorAllowlistTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField("total", "Total", FieldDbType.Currency));
            master.Fields!.Add(new FormField("remark", "Remark", FieldDbType.String));
            var detail = schema.Tables!.Add("OrderLine", "Order line");
            detail.Fields!.Add(new FormField("product", "Product", FieldDbType.String));
            detail.Fields!.Add(new FormField("qty", "Qty", FieldDbType.Integer));
            return schema;
        }

        private IReadOnlyList<ExpressionViolation> Check(string propertyName, string expression)
        {
            var schema = BuildSchema();
            return _calculator.FindAllowlistViolations(schema, schema.MasterTable!, propertyName, expression);
        }

        [Fact]
        [DisplayName("An expression with aggregates and a condition that stay within the allowlist has no finding")]
        public void FindAllowlistViolations_AllowedAggregates_None()
        {
            Assert.Empty(Check(nameof(FormRule.Condition),
                "Sum(OrderLine.qty) > 0 && All(OrderLine, product.Trim().Length > 0) && !IsBlank(remark)"));
        }

        [Fact]
        [DisplayName("A finding after an aggregate is placed where it is written")]
        public void FindAllowlistViolations_AfterAggregate_PositionInWrittenText()
        {
            const string expression = "Count(OrderLine) > 0 && remark.GetHashCode() > 0";

            var violation = Assert.Single(Check(nameof(FormRule.Condition), expression));

            Assert.Equal(expression.IndexOf("GetHashCode", StringComparison.Ordinal), violation.Position);
        }

        [Fact]
        [DisplayName("A finding inside an aggregate's condition is placed where it is written")]
        public void FindAllowlistViolations_InCondition_PositionInWrittenText()
        {
            const string expression = "Any(OrderLine, product.PadLeft(5) == \"x\")";

            var violation = Assert.Single(Check(nameof(FormRule.Condition), expression));

            Assert.Equal(expression.IndexOf("PadLeft", StringComparison.Ordinal), violation.Position);
        }

        [Fact]
        [DisplayName("A text with aggregates nested too deep is reported before any part of it is parsed")]
        public void FindAllowlistViolations_DeepAggregateText_SizeFinding()
        {
            var expression = "Count(OrderLine, " + new string('(', 20) + "qty > 0" + new string(')', 20) + ") > 0";

            var violation = Assert.Single(Check(nameof(FormRule.Condition), expression));

            Assert.Contains("nests deeper", violation.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The whole-schema check lists each finding with its field or rule")]
        public void FindAllowlistViolations_Schema_ListsFieldsAndRules()
        {
            var schema = BuildSchema();
            schema.MasterTable!.Fields!["remark"].ValueExpression = "remark.ToUpper()";
            schema.MasterTable!.Fields!["total"].ValueExpression = "remark.GetHashCode()";
            schema.Rules!.Add(new FormRule("bad", "Any(OrderLine, product.Split(',').Length > 1)", "Bad."));

            var issues = _calculator.FindAllowlistViolations(schema);

            Assert.Contains(issues, issue => issue.FieldName == "total" && issue.Message.Contains("GetHashCode", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.RuleId == "bad" && issue.Message.Contains("Split", StringComparison.Ordinal));
            Assert.DoesNotContain(issues, issue => issue.FieldName == "remark");
        }

        [Theory]
        [DisplayName("The strict check rejects an expression that is too deep, does not parse, or leaves the allowlist")]
        [InlineData("((((((((((((((((((total))))))))))))))))) > 0", "nests deeper")]
        [InlineData("total >", "parse")]
        [InlineData("remark.GetType() == null", "GetType")]
        [InlineData("All(OrderLine, product.Normalize() == \"\")", "Normalize")]
        public void ValidateUntrustedExpression_Rejects(string expression, string expected)
        {
            var schema = BuildSchema();

            var ex = Assert.Throws<ExpressionEvaluationException>(() => _calculator.ValidateUntrustedExpression(
                schema, schema.MasterTable!, nameof(FormRule.Condition), expression));

            Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The strict check accepts an expression within the allowlist")]
        public void ValidateUntrustedExpression_Allowed_Passes()
        {
            var schema = BuildSchema();

            var ex = Record.Exception(() => _calculator.ValidateUntrustedExpression(schema, schema.MasterTable!,
                nameof(FormRule.Condition), "Between(total, 1, 100) && Count(OrderLine, qty > 0) > 0"));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("An evaluator without the allowlist makes every expression a finding, so the strict check refuses it")]
        public void FindAllowlistViolations_EvaluatorWithoutAllowlist_FailsClosed()
        {
            var calculator = new FormExpressionCalculator(new EvaluatorWithoutAllowlist(new DynamicExpressoEvaluator()));
            var schema = BuildSchema();

            Assert.Single(calculator.FindAllowlistViolations(schema, schema.MasterTable!, nameof(FormRule.Condition), "total > 0"));
            Assert.Throws<ExpressionEvaluationException>(() => calculator.ValidateUntrustedExpression(
                schema, schema.MasterTable!, nameof(FormRule.Condition), "total > 0"));
        }

        [Fact]
        [DisplayName("Every expression of the Northwind sample stays within the allowlist")]
        public void FindAllowlistViolations_NorthwindSample_None()
        {
            var folder = Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define", "FormSchema");
            var files = Directory.GetFiles(folder, "*.FormSchema.xml");

            var issues = files
                .Select(file => XmlCodec.DeserializeFromFile<FormSchema>(file)!)
                .SelectMany(schema => _calculator.FindAllowlistViolations(schema)
                    .Select(issue => $"{schema.ProgId} {issue.PropertyName} '{issue.Expression}': {issue.Message}"))
                .ToList();

            Assert.NotEmpty(files);
            Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues));
        }

        private sealed class EvaluatorWithoutAllowlist(IExpressionEvaluator inner) : IExpressionEvaluator
        {
            public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> variables, Type returnType,
                string timeZoneId = "", DateTimeBasis basis = DateTimeBasis.UserZone)
                => inner.Evaluate(expression, variables, returnType, timeZoneId, basis);

            public T Evaluate<T>(string expression, IReadOnlyDictionary<string, object?> variables, string timeZoneId = "",
                DateTimeBasis basis = DateTimeBasis.UserZone)
                => inner.Evaluate<T>(expression, variables, timeZoneId, basis);

            public IReadOnlyList<string> GetReferencedVariables(string expression) => inner.GetReferencedVariables(expression);

            public void Validate(string expression, IReadOnlyDictionary<string, Type> variableTypes, Type returnType)
                => inner.Validate(expression, variableTypes, returnType);
        }
    }
}
