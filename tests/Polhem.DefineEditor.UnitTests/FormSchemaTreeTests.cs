using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The FormSchema tree: the folders the annotations give it, and the Relation / Lookup /
    /// ListItems groups the editor adds under each field.
    /// </summary>
    public sealed class FormSchemaTreeTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-formschema-tree-" + Guid.NewGuid().ToString("N"));
        private readonly FormSchemaDocumentViewModel _document;

        public FormSchemaTreeTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            var customer = table.Fields!.Add("customer_rowid", "Customer", FieldDbType.Guid);
            customer.RelationProgId = "Customer";
            table.Fields!.Add("ref_customer_name", "Customer Name", FieldDbType.String);
            customer.RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_customer_name"));
            var status = table.Fields!.Add("status", "Status", FieldDbType.String);
            status.ListItems!.Add(new ListItem("open", "Open"));
            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            _document = FormSchemaDocumentViewModel.Load(path, SolutionContext.Empty);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private ObjectTreeNode Table => _document.RootNode.Children[0].Children[0];

        private ObjectTreeNode Field(string name) => Table.Children.First(c => c.Value is FormField f && f.FieldName == name);

        private IReadOnlyList<TreeNodeCommand> CommandsFor(ObjectTreeNode node)
        {
            _document.SelectedTreeNode = node;
            return _document.CommandProvider.GetCommands(node);
        }

        [Fact]
        [DisplayName("The schema has Tables and Rules folders, and a table lists its fields directly")]
        public void Tree_HasFoldersAndFieldsUnderTables()
        {
            Assert.Equal(["Tables", "Rules"], _document.RootNode.Children.Select(c => c.Label));
            Assert.Equal(["sys_id", "customer_rowid", "ref_customer_name", "status"], Table.Children.Select(c => ((FormField)c.Value).FieldName));
            Assert.True(Table.IsExpanded);
        }

        [Fact]
        [DisplayName("A field gets a Relation group only when it has a relation, and a ListItems group only when it has items")]
        public void Field_Groups_FollowWhatTheFieldUses()
        {
            Assert.Empty(Field("sys_id").Children);
            var relation = Assert.Single(Field("customer_rowid").Children);
            Assert.Equal("Relation", relation.Label);
            Assert.True(FormSchemaDocumentViewModel.IsRelationGroup(relation));
            Assert.Equal("sys_name -> ref_customer_name", Assert.Single(relation.Children).Label);
            Assert.True(FormSchemaDocumentViewModel.IsListItemsGroup(Assert.Single(Field("status").Children)));
        }

        [Fact]
        [DisplayName("Selecting the Relation group shows the mapping editor, and selecting the ListItems group shows its field")]
        public void SelectedEditorContext_Groups()
        {
            _document.SelectedTreeNode = Field("customer_rowid").Children[0];
            var editor = Assert.IsType<MappingGroupEditor>(_document.SelectedEditorContext);
            Assert.True(editor.IsRelation);

            _document.SelectedTreeNode = Field("status").Children[0];
            Assert.Same(Field("status").Value, _document.SelectedEditorContext);
        }

        [Fact]
        [DisplayName("Add relation mapping on the Relation group adds a mapping and keeps the group selected")]
        public void AddRelationMapping_OnGroup_AddsMapping()
        {
            var group = Field("customer_rowid").Children[0];

            Assert.Equal(LocalizationService.Current["FormSchema_AddRelationMapping"], CommandsFor(group)[0].Label);
            CommandsFor(group)[0].Execute();

            Assert.Equal(2, ((FormField)Field("customer_rowid").Value).RelationFieldMappings!.Count);
            Assert.Equal(2, group.Children.Count);
            Assert.Same(group, _document.SelectedTreeNode);
        }

        [Fact]
        [DisplayName("The schema root offers Generate FormLayout, and the Tables folder offers Add table")]
        public void Commands_RootAndTablesFolder()
        {
            Assert.Equal(LocalizationService.Current["FormSchema_GenerateFormLayout"], Assert.Single(CommandsFor(_document.RootNode)).Label);

            Assert.Single(CommandsFor(_document.RootNode.Children[0])).Execute();

            Assert.Equal(2, _document.Schema.Tables!.Count);
            Assert.Equal(2, _document.RootNode.Children[0].Children.Count);
        }

        [Fact]
        [DisplayName("A mapping and a list item can be deleted, a folder cannot")]
        public void Delete_AvailableForMappingAndListItem()
        {
            Assert.Contains(CommandsFor(Field("customer_rowid").Children[0].Children[0]), c => c.Label == LocalizationService.Current["Action_Delete"]);
            Assert.Contains(CommandsFor(Field("status").Children[0].Children[0]), c => c.Label == LocalizationService.Current["Action_Delete"]);
            Assert.DoesNotContain(CommandsFor(_document.RootNode.Children[1]), c => c.Label == LocalizationService.Current["Action_Delete"]);
        }
    }
}
