using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The expression editor's try-out area lists the fields the expression reads, and shows what the expression gives
    /// for the sample values typed into them.
    /// </summary>
    public sealed class ExpressionTrialTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-expr-trial-" + Guid.NewGuid().ToString("N"));

        public ExpressionTrialTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private FormSchemaDocumentViewModel Load()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("price", "Price", FieldDbType.Decimal);
            table.Fields!.Add("qty", "Qty", FieldDbType.Decimal);
            table.Fields!.Add("note", "Note", FieldDbType.String);
            table.Fields!.Add("amount", "Amount", FieldDbType.Decimal).ValueExpression = "price * qty";
            schema.Rules!.Add(new FormRule("positive_qty", "qty > 0", "Quantity must be positive."));

            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            return FormSchemaDocumentViewModel.Load(path, new SolutionContext(["Order"]));
        }

        private static PropertyDescriptor Property(object component, string name) =>
            TypeDescriptor.GetProperties(component)[name]!;

        private static ExpressionEditorViewModel RuleEditor(FormSchemaDocumentViewModel document, string propertyName)
        {
            var rule = document.Schema.Rules!["positive_qty"];
            return document.CreateExpressionEditor(Property(rule, propertyName), rule)!;
        }

        private static ExpressionEditorViewModel AmountEditor(FormSchemaDocumentViewModel document)
        {
            var amount = document.Schema.Tables!["Order"].Fields!["amount"];
            return document.CreateExpressionEditor(Property(amount, nameof(FormField.ValueExpression)), amount)!;
        }

        private static void Type(ExpressionTrialViewModel trial, string fieldName, string text) =>
            trial.Inputs.Single(i => i.FieldName == fieldName).Text = text;

        [Fact]
        [DisplayName("The inputs are the fields the expression reads, in table order, and follow it as it changes")]
        public void Inputs_FollowReferencedFields()
        {
            var editor = AmountEditor(Load());
            var trial = editor.Trial!;

            Assert.Equal(["price", "qty"], trial.Inputs.Select(i => i.FieldName));

            Type(trial, "qty", "3");
            editor.Expression = "qty * 2 + IsNullOrEmpty(note).CompareTo(false)";

            Assert.Equal(["qty", "note"], trial.Inputs.Select(i => i.FieldName));
            Assert.Equal("3", trial.Inputs[0].Text);
        }

        [Fact]
        [DisplayName("A field expression shows the value it writes, with its type")]
        public void FieldExpression_ShowsValue()
        {
            var trial = AmountEditor(Load()).Trial!;

            Type(trial, "price", "12.5");
            Type(trial, "qty", "4");

            Assert.Equal(ExpressionTrialOutcome.Value, trial.Outcome);
            Assert.StartsWith("50", trial.Result, StringComparison.Ordinal);
            Assert.Contains("decimal", trial.Result, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A condition that fails shows the rule's message as it is being edited")]
        public void Condition_Fails_ShowsMessage()
        {
            var editor = RuleEditor(Load(), nameof(FormRule.Condition));
            var trial = editor.Trial!;

            Type(trial, "qty", "0");
            editor.Message = "Enter a quantity.";

            Assert.Equal(ExpressionTrialOutcome.Failed, trial.Outcome);
            Assert.Contains("Enter a quantity.", trial.Result, StringComparison.Ordinal);

            Type(trial, "qty", "2");

            Assert.Equal(ExpressionTrialOutcome.Passed, trial.Outcome);
        }

        [Fact]
        [DisplayName("An empty box is an empty field, so an empty quantity fails the condition")]
        public void EmptyInput_IsEmptyField()
        {
            var trial = RuleEditor(Load(), nameof(FormRule.Condition)).Trial!;

            Assert.Equal(ExpressionTrialOutcome.Failed, trial.Outcome);
        }

        [Fact]
        [DisplayName("A When expression says whether the rule applies, not whether it passes")]
        public void When_ShowsWhetherRuleApplies()
        {
            var editor = RuleEditor(Load(), nameof(FormRule.When));
            editor.Expression = "price > 100m";
            var trial = editor.Trial!;

            Type(trial, "price", "50");

            Assert.Equal(ExpressionTrialOutcome.Failed, trial.Outcome);
            Assert.Equal(LocalizationService.Current["ExpressionTrial_WhenSkips"], trial.Result);
        }

        [Fact]
        [DisplayName("A sample that cannot be read as its field's type is marked, and nothing is evaluated")]
        public void UnreadableInput_IsMarked()
        {
            var trial = AmountEditor(Load()).Trial!;

            Type(trial, "price", "abc");

            Assert.True(trial.Inputs.Single(i => i.FieldName == "price").HasError);
            Assert.Equal(ExpressionTrialOutcome.None, trial.Outcome);
        }

        [Fact]
        [DisplayName("A division by zero is shown as an error of the expression")]
        public void DivisionByZero_IsError()
        {
            var editor = AmountEditor(Load());
            editor.Expression = "price / qty";
            var trial = editor.Trial!;

            Type(trial, "price", "1");

            Assert.Equal(ExpressionTrialOutcome.Error, trial.Outcome);
            Assert.True(trial.IsProblem);
        }

        [Fact]
        [DisplayName("An expression that does not parse shows no result")]
        public void ParseError_ShowsNothing()
        {
            var editor = AmountEditor(Load());

            editor.Expression = "price *";

            Assert.Equal(ExpressionTrialOutcome.None, editor.Trial!.Outcome);
            Assert.False(editor.Trial.HasResult);
        }
    }
}
