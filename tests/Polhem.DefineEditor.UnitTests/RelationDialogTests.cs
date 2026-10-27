using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The relation dialog's view model over a small solution (Order relates to Customer), and what applying it does to
    /// the FormSchema document and its tree.
    /// </summary>
    public sealed class RelationDialogTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-relation-dialog-" + Guid.NewGuid().ToString("N"));
        private readonly FormSchemaDocumentViewModel _document;
        private readonly SolutionContext _solution;

        public RelationDialogTests()
        {
            var formSchemaDir = Path.Combine(_directory, "FormSchema");
            Directory.CreateDirectory(formSchemaDir);

            var order = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = order.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            table.Fields!.Add("customer_rowid", "Customer", FieldDbType.Guid);
            table.Fields!.Add("amount", "Amount", FieldDbType.Decimal);
            table.Fields!.Add(new FormField("ref_note", "Note", FieldDbType.String, FieldType.RelationField));
            var orderPath = Path.Combine(formSchemaDir, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(order, orderPath);

            var customer = new FormSchema("Customer", "Customer") { CategoryId = "company" };
            var customers = customer.Tables!.Add("Customer", "Customer");
            customers.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
            customers.Fields!.Add("sys_id", "Customer No", FieldDbType.String);
            customers.Fields!.Add("sys_name", "Customer Name", FieldDbType.String);
            customers.Fields!.Add("zone_rowid", "Zone", FieldDbType.Guid);
            var customerPath = Path.Combine(formSchemaDir, "Customer.FormSchema.xml");
            XmlCodec.SerializeToFile(customer, customerPath);

            var programs = new ProgramSettings();
            programs.Items!.Add(new ProgramItem { ProgId = "System", DisplayName = "System" });
            programs.Items!.Add(new ProgramItem { ProgId = "Customer", DisplayName = "Customers" });
            programs.Items!.Add(new ProgramItem { ProgId = "Order", DisplayName = "Orders" });
            XmlCodec.SerializeToFile(programs, Path.Combine(_directory, "ProgramSettings.xml"));

            _solution = new SolutionContext(["Customer", "Order"])
            {
                DefinePath = _directory,
                FormSchemaPaths = new Dictionary<string, string> { ["Customer"] = customerPath, ["Order"] = orderPath },
            };
            _document = FormSchemaDocumentViewModel.Load(orderPath, _solution);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private ObjectTreeNode Table => _document.RootNode.Children[0].Children[0];

        private FormField Field(string name) => _document.Schema.MasterTable!.Fields![name];

        private RelationDialogViewModel Dialog(string fieldName) => new(_document.Schema, Field(fieldName), _solution);

        [Fact]
        [DisplayName("The grid offers the dialog beside a stored field's RelationProgId only")]
        public void ProvideRelationEditor_OnlyStoredFieldRelationProgId()
        {
            var properties = TypeDescriptor.GetProperties(typeof(FormField));

            Assert.NotNull(_document.ProvideRelationEditor(properties[nameof(FormField.RelationProgId)]!, Field("customer_rowid")));
            Assert.Null(_document.ProvideRelationEditor(properties[nameof(FormField.Caption)]!, Field("customer_rowid")));
            Assert.Null(_document.ProvideRelationEditor(properties[nameof(FormField.RelationProgId)]!, Field("ref_note")));
        }

        [Fact]
        [DisplayName("The picker lists ProgramSettings with names and filters by keyword")]
        public void Picker_ListsProgramSettingsAndFilters()
        {
            var dialog = Dialog("customer_rowid");

            Assert.Equal(["System", "Customer", "Order"], dialog.Choices.Select(c => c.ProgId));
            Assert.False(dialog.IsProgramSettingsMissing);

            dialog.SearchText = "custom";

            Assert.Equal(["Customer"], dialog.FilteredChoices.Select(c => c.ProgId));
        }

        [Fact]
        [DisplayName("Typing a listed ProgId lists its master fields; typed text that names no form cannot be applied")]
        public void SearchText_SelectsFormOrBlocks()
        {
            var dialog = Dialog("customer_rowid");

            dialog.SearchText = "Customer";

            Assert.Equal("Customer", dialog.ProgId);
            Assert.Equal("Customers", dialog.SelectedName);
            Assert.Equal(["sys_id", "sys_name", "zone_rowid"], dialog.Rows.Select(r => r.SourceField));
            Assert.True(dialog.ApplyCommand.CanExecute(null));

            dialog.SearchText = "Custo";

            Assert.Empty(dialog.Rows);
            Assert.False(dialog.ApplyCommand.CanExecute(null));
        }

        [Fact]
        [DisplayName("A program without a FormSchema cannot be applied, and a row error disables applying")]
        public void Apply_BlockedByMissingSchemaOrRowError()
        {
            var dialog = Dialog("customer_rowid");

            dialog.SelectedChoice = dialog.Choices.Single(c => c.ProgId == "System");
            Assert.False(dialog.ApplyCommand.CanExecute(null));

            dialog.SelectedChoice = dialog.Choices.Single(c => c.ProgId == "Customer");
            Assert.True(dialog.ApplyCommand.CanExecute(null));
            dialog.Rows[0].DestinationField = "customer_rowid";
            Assert.False(dialog.ApplyCommand.CanExecute(null));
            Assert.Equal(RelationSetupRowStatus.DestinationIsForeignKey, dialog.Rows[0].Status);
        }

        [Fact]
        [DisplayName("A source the lookup does not return turns the server callback on, and the message says the callback fills it")]
        public void SourceMissingFromLookup_TurnsCallbackOn()
        {
            var dialog = Dialog("customer_rowid");
            dialog.SearchText = "Customer";
            Assert.False(dialog.ValueChangeCallback);

            dialog.Rows.Single(r => r.SourceField == "zone_rowid").IsIncluded = true;

            Assert.True(dialog.ValueChangeCallback);
            Assert.Contains(dialog.Messages, m => m.Contains("zone_rowid", StringComparison.Ordinal)
                && m.Contains("callback", StringComparison.OrdinalIgnoreCase));
            _document.ApplyRelationSetup(dialog);
            Assert.True(Field("customer_rowid").ValueChangeCallback);
        }

        [Fact]
        [DisplayName("Once the user turns the callback off it stays off, and a source the lookup does not return is reported")]
        public void CallbackTurnedOff_StaysOff_ReportsMissingSource()
        {
            var dialog = Dialog("customer_rowid");
            dialog.SearchText = "Customer";
            var zone = dialog.Rows.Single(r => r.SourceField == "zone_rowid");
            zone.IsIncluded = true;

            dialog.ValueChangeCallback = false;
            zone.LookupOnly = true;

            Assert.False(dialog.ValueChangeCallback);
            Assert.Contains(dialog.Messages, m => m.Contains("zone_rowid", StringComparison.Ordinal)
                && m.Contains("LookupFields", StringComparison.Ordinal)
                && !m.Contains("fills", StringComparison.Ordinal));
            _document.ApplyRelationSetup(dialog);
            Assert.False(Field("customer_rowid").ValueChangeCallback);
        }

        [Fact]
        [DisplayName("A field that already calls back keeps the flag, and removing the relation leaves it unchanged")]
        public void ExistingCallback_IsKept_AndRemovalLeavesIt()
        {
            Field("customer_rowid").ValueChangeCallback = true;
            var dialog = Dialog("customer_rowid");
            dialog.SearchText = "Customer";
            Assert.True(dialog.ValueChangeCallback);
            _document.ApplyRelationSetup(dialog);

            var again = Dialog("customer_rowid");
            again.SearchText = "";
            Assert.False(again.IsRelating);
            _document.ApplyRelationSetup(again);

            Assert.Equal(string.Empty, Field("customer_rowid").RelationProgId);
            Assert.True(Field("customer_rowid").ValueChangeCallback);
        }

        [Fact]
        [DisplayName("Applying writes the relation, adds the new fields' nodes after the foreign key and rebuilds its Relation group")]
        public void ApplyRelationSetup_UpdatesDocumentAndTree()
        {
            var dialog = Dialog("customer_rowid");
            dialog.SearchText = "Customer";

            var changes = _document.ApplyRelationSetup(dialog);

            Assert.Equal(["ref_customer_id", "ref_customer_name"], changes.AddedFields.Select(f => f.FieldName));
            Assert.Equal(["sys_id", "customer_rowid", "ref_customer_id", "ref_customer_name", "amount", "ref_note"],
                Table.Children.Select(c => ((FormField)c.Value).FieldName));
            var fieldNode = Table.Children[1];
            var group = Assert.Single(fieldNode.Children);
            Assert.True(FormSchemaDocumentViewModel.IsRelationGroup(group));
            Assert.Equal(2, group.Children.Count);
            Assert.True(_document.IsDirty);
            Assert.Equal("Customer", Field("customer_rowid").RelationProgId);
        }

        [Fact]
        [DisplayName("Reopening the dialog shows the applied mappings, and clearing the form removes the relation")]
        public void Reopen_ShowsMappingsAndClearingRemoves()
        {
            var first = Dialog("customer_rowid");
            first.SearchText = "Customer";
            _document.ApplyRelationSetup(first);

            var again = Dialog("customer_rowid");
            Assert.Equal("Customer", again.ProgId);
            Assert.Equal(["sys_id", "sys_name"], again.Rows.Where(r => r.IsIncluded).Select(r => r.SourceField));
            Assert.All(again.Rows.Where(r => r.IsIncluded), r => Assert.Equal(RelationSetupRowStatus.Existing, r.Status));

            again.SearchText = "";
            Assert.True(again.IsRemoving);
            Assert.Equal(["ref_customer_id", "ref_customer_name"], again.Orphans);
            again.RemoveOrphans = true;
            Assert.True(again.ApplyCommand.CanExecute(null));
            _document.ApplyRelationSetup(again);

            Assert.Equal(string.Empty, Field("customer_rowid").RelationProgId);
            Assert.Empty(Table.Children[1].Children);
            Assert.Equal(["sys_id", "customer_rowid", "amount", "ref_note"], Table.Children.Select(c => ((FormField)c.Value).FieldName));
        }
    }
}
