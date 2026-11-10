using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The FormSchema editor opens an expression editor beside each expression property, with the variables and
    /// result type the expression is evaluated with, and writes back what the user confirms.
    /// </summary>
    public sealed class ExpressionEditorTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-expr-editor-" + Guid.NewGuid().ToString("N"));

        public ExpressionEditorTests()
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
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            master.Fields!.Add("amount", "Amount", FieldDbType.Decimal);
            master.Fields!.Add("tax", "Tax", FieldDbType.Decimal).ValueExpression = "amount * 0.05m";
            var detail = schema.Tables!.Add("OrderLine", "Order Line");
            detail.Fields!.Add("quantity", "Quantity", FieldDbType.Integer);
            schema.Rules!.Add(new FormRule("positive_amount", "amount > 0", "Amount must be positive."));
            schema.Rules!.Add(new FormRule("positive_quantity", "quantity > 0", "Quantity must be positive.")
            {
                TargetTable = "OrderLine",
            });
            schema.Rules!.Add(new FormRule("orphan", "true", "No table.") { TargetTable = "Missing" });

            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            return FormSchemaDocumentViewModel.Load(path, new SolutionContext(["Order"]));
        }

        private static PropertyDescriptor Property(object component, string name) =>
            TypeDescriptor.GetProperties(component)[name]!;

        [Theory]
        [InlineData(nameof(FormRule.When))]
        [InlineData(nameof(FormRule.Condition))]
        [DisplayName("A rule's When and Condition get an expression editor")]
        public void ProvideValueEditor_RuleExpression_ReturnsEditor(string propertyName)
        {
            var document = Load();
            var rule = document.Schema.Rules!["positive_amount"];

            Assert.NotNull(document.ProvideValueEditor(Property(rule, propertyName), rule));
        }

        [Theory]
        [InlineData(nameof(FormField.ValueExpression))]
        [InlineData(nameof(FormField.DefaultValueExpression))]
        [DisplayName("A field's ValueExpression and DefaultValueExpression get an expression editor")]
        public void ProvideValueEditor_FieldExpression_ReturnsEditor(string propertyName)
        {
            var document = Load();
            var field = document.Schema.Tables!["Order"].Fields!["tax"];

            Assert.NotNull(document.ProvideValueEditor(Property(field, propertyName), field));
        }

        [Fact]
        [DisplayName("A rule whose target table is missing gets no expression editor, as validation skips it")]
        public void CreateExpressionEditor_RuleWithMissingTable_ReturnsNull()
        {
            var document = Load();
            var rule = document.Schema.Rules!["orphan"];

            Assert.Null(document.CreateExpressionEditor(Property(rule, nameof(FormRule.Condition)), rule));
        }

        [Fact]
        [DisplayName("The editor of a detail-table rule lists that table's fields, not the master's")]
        public void CreateExpressionEditor_DetailRule_ListsDetailFields()
        {
            var document = Load();
            var rule = document.Schema.Rules!["positive_quantity"];

            var editor = document.CreateExpressionEditor(Property(rule, nameof(FormRule.Condition)), rule)!;

            Assert.Equal(["quantity"], editor.Fields.Select(f => f.Name));
            Assert.False(editor.HasError);
        }

        [Fact]
        [DisplayName("The editor lists the helper functions, a function without parameters inserted with its parentheses")]
        public void CreateExpressionEditor_Functions_ListsHelpers()
        {
            var document = Load();
            var field = document.Schema.Tables!["Order"].Fields!["tax"];

            var editor = document.CreateExpressionEditor(Property(field, nameof(FormField.DefaultValueExpression)), field)!;

            Assert.Equal("Today()", editor.Functions.Single(f => f.Name == "Today").InsertText);
            Assert.Equal("IsNullOrEmpty(", editor.Functions.Single(f => f.Name == "IsNullOrEmpty").InsertText);
        }

        [Fact]
        [DisplayName("The symbol tree has the fields and then the functions as its two headings")]
        public void CreateExpressionEditor_SymbolGroups_FieldsThenFunctions()
        {
            var document = Load();
            var field = document.Schema.Tables!["Order"].Fields!["tax"];

            var editor = document.CreateExpressionEditor(Property(field, nameof(FormField.ValueExpression)), field)!;

            Assert.Collection(editor.SymbolGroups,
                group => Assert.Same(editor.Fields, group.Items),
                group => Assert.Same(editor.Functions, group.Items));
        }

        [Fact]
        [DisplayName("A rule condition that does not produce a boolean is an error, with where parsing stopped")]
        public void Expression_UnknownIdentifier_ReportsErrorAndPosition()
        {
            var document = Load();
            var rule = document.Schema.Rules!["positive_amount"];
            var editor = document.CreateExpressionEditor(Property(rule, nameof(FormRule.Condition)), rule)!;

            editor.Expression = "amount > limit";

            Assert.True(editor.HasError);
            Assert.Equal("amount > limit".IndexOf("limit", StringComparison.Ordinal), editor.ErrorPosition);

            editor.Expression = string.Empty;

            Assert.False(editor.HasError);
            Assert.Null(editor.ErrorPosition);
        }

        [Fact]
        [DisplayName("Confirming writes the condition and the message edited beside it back to the rule")]
        public void ApplyExpressionEdit_ConditionAndMessage_WritesBoth()
        {
            var document = Load();
            var rule = document.Schema.Rules!["positive_amount"];
            var property = Property(rule, nameof(FormRule.Condition));
            var editor = document.CreateExpressionEditor(property, rule)!;
            Assert.True(editor.HasMessage);

            editor.Expression = "amount >= 0";
            editor.Message = "Amount cannot be negative.";

            Assert.True(FormSchemaDocumentViewModel.ApplyExpressionEdit(editor, property, rule));
            Assert.Equal("amount >= 0", rule.Condition);
            Assert.Equal("Amount cannot be negative.", rule.Message);
        }

        [Fact]
        [DisplayName("Confirming without a change reports nothing changed, and the When editor leaves the message alone")]
        public void ApplyExpressionEdit_Unchanged_ReturnsFalse()
        {
            var document = Load();
            var rule = document.Schema.Rules!["positive_amount"];
            var property = Property(rule, nameof(FormRule.When));
            var editor = document.CreateExpressionEditor(property, rule)!;

            Assert.False(editor.HasMessage);
            Assert.False(FormSchemaDocumentViewModel.ApplyExpressionEdit(editor, property, rule));
            Assert.Equal("Amount must be positive.", rule.Message);
        }
    }
}
