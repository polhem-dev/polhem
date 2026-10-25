using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for <see cref="FormExpressionCalculator.ValidateExpressions"/>: every field and rule expression is parsed
    /// against the field types of its table, and each one that does not parse is reported with where it lives.
    /// </summary>
    public class FormExpressionCalculatorValidationTests
    {
        private readonly FormExpressionCalculator _calculator = new(new DynamicExpressoEvaluator());

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField("sys_rowid", "RowId", FieldDbType.Guid));
            master.Fields!.Add(new FormField("customer_rowid", "Customer", FieldDbType.Guid));
            master.Fields!.Add(new FormField("order_date", "Order Date", FieldDbType.Date)
            {
                DefaultValueExpression = "Today()",
            });
            master.Fields!.Add(new FormField("created_at", "Created At", FieldDbType.DateTime)
            {
                DefaultValueExpression = "Now()",
            });
            master.Fields!.Add(new FormField("amount", "Amount", FieldDbType.Decimal));
            master.Fields!.Add(new FormField("tax", "Tax", FieldDbType.Decimal) { ValueExpression = "amount * 0.05m" });

            var detail = schema.Tables!.Add("OrderLine", "Order Line");
            detail.Fields!.Add(new FormField("quantity", "Quantity", FieldDbType.Integer));
            detail.Fields!.Add(new FormField("unit_price", "Unit Price", FieldDbType.Decimal));
            detail.Fields!.Add(new FormField("line_total", "Line Total", FieldDbType.Decimal)
            {
                ValueExpression = "quantity * unit_price",
            });

            schema.Rules!.Add(new FormRule("has_customer", "customer_rowid != Guid.Empty", "Pick a customer."));
            schema.Rules!.Add(new FormRule("positive_quantity", "quantity > 0", "Quantity must be positive.")
            {
                TargetTable = "OrderLine",
            });
            return schema;
        }

        [Fact]
        [DisplayName("ValidateExpressions reports nothing for a schema whose expressions all parse")]
        public void ValidateExpressions_ValidSchema_ReturnsNoIssues()
        {
            Assert.Empty(_calculator.ValidateExpressions(BuildSchema()));
        }

        [Fact]
        [DisplayName("ValidateExpressions reports a decimal ValueExpression multiplied by a double literal on its field")]
        public void ValidateExpressions_DecimalTimesDouble_ReportsField()
        {
            var schema = BuildSchema();
            schema.Tables!["Order"].Fields!["tax"].ValueExpression = "amount * 1.05";

            var issue = Assert.Single(_calculator.ValidateExpressions(schema));

            Assert.Equal("Order", issue.TableName);
            Assert.Equal("tax", issue.FieldName);
            Assert.Null(issue.RuleId);
            Assert.Equal(nameof(FormField.ValueExpression), issue.PropertyName);
            Assert.Equal("amount * 1.05", issue.Expression);
            Assert.Contains("Decimal", issue.Message);
            Assert.Contains("Double", issue.Message);
        }

        [Fact]
        [DisplayName("ValidateExpressions reports a DefaultValueExpression that references a field of another table")]
        public void ValidateExpressions_DefaultValueReferencesOtherTable_ReportsField()
        {
            var schema = BuildSchema();
            schema.Tables!["OrderLine"].Fields!["unit_price"].DefaultValueExpression = "amount";

            var issue = Assert.Single(_calculator.ValidateExpressions(schema));

            Assert.Equal("OrderLine", issue.TableName);
            Assert.Equal("unit_price", issue.FieldName);
            Assert.Equal(nameof(FormField.DefaultValueExpression), issue.PropertyName);
        }

        [Fact]
        [DisplayName("ValidateExpressions parses a rule against the table it targets and requires a bool result")]
        public void ValidateExpressions_RuleConditionNotBool_ReportsRule()
        {
            var schema = BuildSchema();
            schema.Rules!["positive_quantity"].When = "unit_price";

            var issue = Assert.Single(_calculator.ValidateExpressions(schema));

            Assert.Equal("OrderLine", issue.TableName);
            Assert.Null(issue.FieldName);
            Assert.Equal("positive_quantity", issue.RuleId);
            Assert.Equal(nameof(FormRule.When), issue.PropertyName);
        }

        [Fact]
        [DisplayName("ValidateExpressions reports a master-table rule that references a detail field")]
        public void ValidateExpressions_MasterRuleReferencesDetailField_ReportsRule()
        {
            var schema = BuildSchema();
            schema.Rules!["has_customer"].Condition = "quantity > 0";

            var issue = Assert.Single(_calculator.ValidateExpressions(schema));

            Assert.Equal("Order", issue.TableName);
            Assert.Equal("has_customer", issue.RuleId);
            Assert.Equal(nameof(FormRule.Condition), issue.PropertyName);
        }

        [Theory]
        [InlineData(FieldDbType.String)]
        [InlineData(FieldDbType.Text)]
        [InlineData(FieldDbType.Boolean)]
        [InlineData(FieldDbType.AutoIncrement)]
        [InlineData(FieldDbType.Short)]
        [InlineData(FieldDbType.Integer)]
        [InlineData(FieldDbType.Long)]
        [InlineData(FieldDbType.Decimal)]
        [InlineData(FieldDbType.Currency)]
        [InlineData(FieldDbType.Date)]
        [InlineData(FieldDbType.DateTime)]
        [InlineData(FieldDbType.Guid)]
        [InlineData(FieldDbType.Binary)]
        [InlineData(FieldDbType.Time)]
        [DisplayName("ValidateExpressions accepts a field referenced by name, whatever its database type")]
        public void ValidateExpressions_FieldOfEachDbType_Parses(FieldDbType dbType)
        {
            var schema = new FormSchema("Probe", "Probe");
            var table = schema.Tables!.Add("Probe", "Probe");
            table.Fields!.Add(new FormField("source", "Source", dbType));
            table.Fields!.Add(new FormField("copy", "Copy", dbType) { ValueExpression = "source" });

            Assert.Empty(_calculator.ValidateExpressions(schema));
        }
    }
}
