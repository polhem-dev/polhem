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
        [DisplayName("A rule whose target table is missing opens with that table named as missing, and cannot be confirmed")]
        public void CreateRuleDialog_RuleWithMissingTable_CannotConfirm()
        {
            var document = Load();
            using var dialog = document.CreateRuleDialog(document.Schema.Rules!["orphan"]);

            Assert.Null(dialog.SelectedTable.Table);
            Assert.Equal("Missing", dialog.SelectedTable.TargetTable);
            Assert.False(dialog.CanConfirm);
        }

        [Fact]
        [DisplayName("The dialog of a detail-table rule lists that table's fields, and a new table checks the expressions again")]
        public void CreateRuleDialog_DetailRule_ListsDetailFieldsAndFollowsTable()
        {
            var document = Load();
            using var dialog = document.CreateRuleDialog(document.Schema.Rules!["positive_quantity"]);

            Assert.Equal(["quantity"], dialog.Symbols.Fields.Select(f => f.Name));
            Assert.False(dialog.Condition.HasError);

            dialog.SelectedTable = dialog.Tables.Single(t => t.TargetTable.Length == 0);

            Assert.Contains("amount", dialog.Symbols.Fields.Select(f => f.Name));
            Assert.True(dialog.Condition.HasError);
            Assert.False(dialog.CanConfirm);
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
        [DisplayName("The symbol tree has the fields, the functions and the types as headings, each type listing its static members")]
        public void CreateExpressionEditor_SymbolGroups_FieldsFunctionsTypes()
        {
            var document = Load();
            var field = document.Schema.Tables!["Order"].Fields!["tax"];

            var editor = document.CreateExpressionEditor(Property(field, nameof(FormField.ValueExpression)), field)!;

            Assert.Collection(editor.SymbolGroups,
                group => Assert.Same(editor.Fields, group.Items),
                group => Assert.Same(editor.Functions, group.Items),
                group => Assert.Same(editor.Types, group.Items));
            Assert.All(editor.SymbolGroups, group => Assert.True(group.IsExpanded));
            var math = editor.Types.Single(t => t.Title == "Math");
            Assert.False(math.IsExpanded);
            Assert.Contains(math.Items.OfType<ExpressionSymbol>(), m => m.InsertText == "Math.Round(");
        }

        [Fact]
        [DisplayName("A rule condition that does not produce a boolean is an error, with where parsing stopped")]
        public void Expression_UnknownIdentifier_ReportsErrorAndPosition()
        {
            var document = Load();
            using var dialog = document.CreateRuleDialog(document.Schema.Rules!["positive_amount"]);
            var editor = dialog.Condition;

            editor.Expression = "amount > limit";

            Assert.True(editor.HasError);
            Assert.Equal("amount > limit".IndexOf("limit", StringComparison.Ordinal), editor.ErrorPosition);

            editor.Expression = string.Empty;

            Assert.False(editor.HasError);
            Assert.Null(editor.ErrorPosition);
        }

        [Fact]
        [DisplayName("An expression outside the allowlist shows a warning, not an error, and an allowed one shows neither")]
        public void Expression_OutsideAllowlist_ShowsWarning()
        {
            var document = Load();
            using var dialog = document.CreateRuleDialog(document.Schema.Rules!["positive_amount"]);
            var editor = dialog.Condition;

            editor.Expression = "sys_id.GetHashCode() > 0";

            Assert.False(editor.HasError);
            Assert.True(editor.HasWarning);
            Assert.Contains("GetHashCode", editor.Warning, StringComparison.Ordinal);

            editor.Expression = "sys_id.Length > 0";

            Assert.False(editor.HasWarning);
        }

        [Fact]
        [DisplayName("The editor lists no member outside the allowlist under a type")]
        public void CreateExpressionEditor_Types_ListOnlyAllowedMembers()
        {
            var document = Load();
            var field = document.Schema.Tables!["Order"].Fields!["tax"];

            var editor = document.CreateExpressionEditor(Property(field, nameof(FormField.ValueExpression)), field)!;

            var names = editor.Types.SelectMany(group => group.Items.OfType<ExpressionSymbol>()).Select(s => s.InsertText).ToList();
            Assert.Contains("Math.Round(", names);
            Assert.DoesNotContain(names, name => name.StartsWith("Guid.NewGuid", StringComparison.Ordinal));
            Assert.DoesNotContain(names, name => name.StartsWith("DateTime.Now", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A detail aggregate in a master field's value expression is accepted, as form validation accepts it")]
        public void Expression_MasterAggregate_NoError()
        {
            var document = Load();
            var field = document.Schema.Tables!["Order"].Fields!["amount"];
            var editor = document.CreateExpressionEditor(Property(field, nameof(FormField.ValueExpression)), field)!;

            editor.Expression = "Count(OrderLine) * 10m";

            Assert.False(editor.HasError);
        }

        [Fact]
        [DisplayName("An error after a detail aggregate is underlined where it is in the text as written")]
        public void Expression_ErrorAfterAggregate_PositionInWrittenText()
        {
            var document = Load();
            using var dialog = document.CreateRuleDialog(document.Schema.Rules!["positive_amount"]);
            var editor = dialog.Condition;

            editor.Expression = "Count(OrderLine) > limit";

            Assert.True(editor.HasError);
            Assert.Equal("Count(OrderLine) > limit".IndexOf("limit", StringComparison.Ordinal), editor.ErrorPosition);
        }

        [Fact]
        [DisplayName("Confirming the rule dialog writes the whole rule back, and reports whether anything changed")]
        public void RuleDialog_ApplyTo_WritesRule()
        {
            var document = Load();
            var rule = document.Schema.Rules!["positive_amount"];
            using var dialog = document.CreateRuleDialog(rule);

            Assert.False(dialog.ApplyTo(rule));

            dialog.When.Expression = "amount > 100m";
            dialog.Condition.Expression = "tax > 0";
            dialog.Message = "Tax is required above 100.";
            dialog.Trigger = FormRuleTrigger.BeforeDelete;

            Assert.True(dialog.ApplyTo(rule));
            Assert.Equal("amount > 100m", rule.When);
            Assert.Equal("tax > 0", rule.Condition);
            Assert.Equal("Tax is required above 100.", rule.Message);
            Assert.Equal(FormRuleTrigger.BeforeDelete, rule.Trigger);
            Assert.Equal("positive_amount", rule.RuleId);
        }

        [Fact]
        [DisplayName("A new rule needs a RuleId no other rule has and a Condition before it can be confirmed")]
        public void RuleDialog_NewRule_NeedsUniqueIdAndCondition()
        {
            var document = Load();
            using var dialog = document.CreateRuleDialog(null, "new_rule");

            Assert.True(dialog.IsNew);
            Assert.False(dialog.CanConfirm);

            dialog.Condition.Expression = "amount > 0";
            Assert.True(dialog.CanConfirm);

            dialog.RuleId = "POSITIVE_AMOUNT";
            Assert.False(dialog.CanConfirm);
            Assert.Contains("POSITIVE_AMOUNT", dialog.Problem, StringComparison.Ordinal);

            dialog.RuleId = "amount_positive";
            var rule = dialog.BuildRule();
            Assert.Equal("amount_positive", rule.RuleId);
            Assert.Equal(string.Empty, rule.TargetTable);
            Assert.Equal("amount > 0", rule.Condition);
        }

        [Fact]
        [DisplayName("A field editor leaves an unchanged expression alone and writes a changed one")]
        public void ApplyExpressionEdit_FieldExpression_WritesOnlyChanges()
        {
            var document = Load();
            var field = document.Schema.Tables!["Order"].Fields!["tax"];
            var property = Property(field, nameof(FormField.ValueExpression));
            var editor = document.CreateExpressionEditor(property, field)!;

            Assert.False(FormSchemaDocumentViewModel.ApplyExpressionEdit(editor, property, field));

            editor.Expression = "amount * 0.1m";

            Assert.True(FormSchemaDocumentViewModel.ApplyExpressionEdit(editor, property, field));
            Assert.Equal("amount * 0.1m", field.ValueExpression);
        }
    }
}
