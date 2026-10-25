using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The FormSchema editor's validation parses every field and rule expression, so an expression that would
    /// fail at run time is listed as an error, with the field or rule that holds it, before the file is saved.
    /// </summary>
    public sealed class FormSchemaExpressionValidationTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-formschema-expr-" + Guid.NewGuid().ToString("N"));

        public FormSchemaExpressionValidationTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private FormSchemaDocumentViewModel Load(string taxExpression, string ruleCondition)
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            table.Fields!.Add("amount", "Amount", FieldDbType.Decimal);
            table.Fields!.Add("tax", "Tax", FieldDbType.Decimal).ValueExpression = taxExpression;
            schema.Rules!.Add(new FormRule("positive_amount", ruleCondition, "Amount must be positive."));

            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            return FormSchemaDocumentViewModel.Load(path, new SolutionContext(["Order"]));
        }

        [Fact]
        [DisplayName("Validate lists a decimal ValueExpression multiplied by a double literal as an error on its field")]
        public void Validate_DecimalTimesDouble_ListsFieldError()
        {
            var document = Load("amount * 1.05", "amount > 0");

            document.ValidateCommand.Execute(null);

            var issue = Assert.Single(document.Issues);
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Equal("Order.tax.ValueExpression", issue.Path);
            Assert.Contains("Decimal", issue.Message);
            Assert.Contains("Double", issue.Message);
        }

        [Fact]
        [DisplayName("Validate lists a rule condition that does not parse as an error on its rule")]
        public void Validate_RuleConditionReferencesUnknownField_ListsRuleError()
        {
            var document = Load("amount * 1.05m", "total > 0");

            document.ValidateCommand.Execute(null);

            var issue = Assert.Single(document.Issues);
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Equal("Rules.positive_amount.Condition", issue.Path);
        }

        [Fact]
        [DisplayName("Validate lists no expression issue when every expression parses")]
        public void Validate_ValidExpressions_ListsNoIssues()
        {
            var document = Load("amount * 1.05m", "amount > 0");

            document.ValidateCommand.Execute(null);

            Assert.Empty(document.Issues);
        }
    }
}
