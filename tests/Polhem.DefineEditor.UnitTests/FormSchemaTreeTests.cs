using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The FormSchema tree: the folders the annotations give it, the Relation / Lookup groups the
    /// editor adds under each field, and what the property grid is given for them.
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

            var related = new FormSchema("Customer", "Customer") { CategoryId = "company" };
            var customers = related.Tables!.Add("Customer", "Customer");
            customers.Fields!.Add("sys_id", "Customer No", FieldDbType.String);
            customers.Fields!.Add("sys_name", "Customer Name", FieldDbType.String);
            var relatedPath = Path.Combine(_directory, "Customer.FormSchema.xml");
            XmlCodec.SerializeToFile(related, relatedPath);

            var solution = new SolutionContext(["Customer", "Order"])
            {
                FormSchemaPaths = new Dictionary<string, string> { ["Customer"] = relatedPath, ["Order"] = path },
                LanguagePaths =
                [
                    SaveLanguage("Order", "en-US", "OrderStatus"),
                    SaveLanguage("Order", "zh-TW", "OrderStatus"),
                    SaveLanguage("Common", "en-US", "Gender"),
                    Path.Combine(_directory, "Missing.Language.xml"),
                ],
            };
            _document = FormSchemaDocumentViewModel.Load(path, solution);
        }

        private string SaveLanguage(string @namespace, string lang, string enumName)
        {
            var resource = new LanguageResource { Namespace = @namespace, Lang = lang };
            resource.Enums.Add(new LanguageEnum { Name = enumName });
            var path = Path.Combine(_directory, $"{@namespace}.{lang}.Language.xml");
            XmlCodec.SerializeToFile(resource, path);
            return path;
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
        [DisplayName("A field gets a Relation group only when it has a relation, and no group for its list items")]
        public void Field_Groups_FollowWhatTheFieldUses()
        {
            Assert.Empty(Field("sys_id").Children);
            var relation = Assert.Single(Field("customer_rowid").Children);
            Assert.Equal("Relation", relation.Label);
            Assert.True(FormSchemaDocumentViewModel.IsRelationGroup(relation));
            Assert.Equal("sys_name -> ref_customer_name", Assert.Single(relation.Children).Label);
            Assert.Empty(Field("status").Children);
        }

        [Fact]
        [DisplayName("Selecting the Relation group shows its field narrowed to the Relation category, and the field node shows all of it")]
        public void SelectedEditorContext_RelationGroup_ShowsFieldRelationCategory()
        {
            _document.SelectedTreeNode = Field("customer_rowid").Children[0];

            Assert.Same(Field("customer_rowid").Value, _document.SelectedEditorContext);
            var filter = Assert.IsType<Func<PropertyDescriptor, bool>>(_document.SelectedPropertyFilter);
            var shown = TypeDescriptor.GetProperties(typeof(FormField)).Cast<PropertyDescriptor>().Where(filter).Select(p => p.Name).ToList();
            Assert.Contains(nameof(FormField.RelationProgId), shown);
            Assert.Contains(nameof(FormField.LookupFieldMappings), shown);
            Assert.DoesNotContain(nameof(FormField.FieldName), shown);

            _document.SelectedTreeNode = Field("customer_rowid");

            Assert.Null(_document.SelectedPropertyFilter);
        }

        [Fact]
        [DisplayName("Add relation mapping on the Relation group adds a mapping and selects it")]
        public void AddRelationMapping_OnGroup_AddsMapping()
        {
            var group = Field("customer_rowid").Children[0];

            Assert.Equal(LocalizationService.Current["FormSchema_AddRelationMapping"], CommandsFor(group)[0].Label);
            CommandsFor(group)[0].Execute();

            Assert.Equal(2, ((FormField)Field("customer_rowid").Value).RelationFieldMappings!.Count);
            Assert.Equal(2, group.Children.Count);
            Assert.Same(group.Children[1], _document.SelectedTreeNode);
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
        [DisplayName("A mapping can be deleted, a folder cannot")]
        public void Delete_AvailableForMapping()
        {
            Assert.Contains(CommandsFor(Field("customer_rowid").Children[0].Children[0]), c => c.Label == LocalizationService.Current["Action_Delete"]);
            Assert.DoesNotContain(CommandsFor(_document.RootNode.Children[1]), c => c.Label == LocalizationService.Current["Action_Delete"]);
        }

        [Fact]
        [DisplayName("RelationProgId suggests the solution's form ids, and a mapping suggests its table's fields and the related form's fields")]
        public void Suggest_ProgIdsAndMappingFields()
        {
            var customer = (FormField)Field("customer_rowid").Value;
            var properties = TypeDescriptor.GetProperties(typeof(FormField));
            Assert.Equal(["Customer", "Order"], _document.Suggest(properties[nameof(FormField.RelationProgId)]!, customer));
            Assert.Null(_document.Suggest(properties[nameof(FormField.Caption)]!, customer));

            var mappingNode = Field("customer_rowid").Children[0].Children[0];
            _document.SelectedTreeNode = mappingNode;
            var mappingProperties = TypeDescriptor.GetProperties(typeof(FieldMapping));

            Assert.Equal(["sys_id", "customer_rowid", "ref_customer_name", "status"],
                _document.Suggest(mappingProperties[nameof(FieldMapping.DestinationField)]!, mappingNode.Value));
            Assert.Equal(["sys_id", "sys_name"],
                _document.Suggest(mappingProperties[nameof(FieldMapping.SourceField)]!, mappingNode.Value));
        }

        [Fact]
        [DisplayName("LangEnumName suggests this form's enums by bare name, then every solution enum by its full name, once each")]
        public void Suggest_LangEnumName_OwnBareNamesThenQualified()
        {
            var status = (FormField)Field("status").Value;
            var property = TypeDescriptor.GetProperties(typeof(FormField))[nameof(FormField.LangEnumName)]!;

            Assert.Equal(["OrderStatus", "Common.Gender", "Order.OrderStatus"], _document.Suggest(property, status));
        }

        [Fact]
        [DisplayName("Opening a field's relation mappings from the grid selects their tree group and reports no change")]
        public async Task EditMappingsInTree_SelectsGroup()
        {
            var customer = (FormField)Field("customer_rowid").Value;
            _document.SelectedTreeNode = Field("customer_rowid");

            var edit = _document.EditMappingsInTree(new(customer, TypeDescriptor.GetProperties(customer)[nameof(FormField.RelationFieldMappings)]!));

            Assert.NotNull(edit);
            Assert.False(await edit);
            Assert.Same(Field("customer_rowid").Children[0], _document.SelectedTreeNode);
            Assert.Null(_document.EditMappingsInTree(new(customer, TypeDescriptor.GetProperties(customer)[nameof(FormField.ListItems)]!)));
        }

        [Fact]
        [DisplayName("Opening the lookup mappings of a field without a Lookup group adds the group after the Relation group")]
        public async Task EditMappingsInTree_AddsMissingGroup()
        {
            var customer = (FormField)Field("customer_rowid").Value;
            _document.SelectedTreeNode = Field("customer_rowid");

            Assert.False(await _document.EditMappingsInTree(new(customer, TypeDescriptor.GetProperties(customer)[nameof(FormField.LookupFieldMappings)]!))!);

            Assert.Equal(["Relation", "Lookup"], Field("customer_rowid").Children.Select(c => c.Label));
            Assert.True(FormSchemaDocumentViewModel.IsLookupGroup(_document.SelectedTreeNode!));
        }

        [Fact]
        [DisplayName("Setting a RelationProgId through the grid adds the field's Relation group")]
        public void OnPropertyEdited_RelationProgIdSet_AddsGroup()
        {
            var field = (FormField)Field("sys_id").Value;
            _document.SelectedTreeNode = Field("sys_id");

            field.RelationProgId = "Customer";
            _document.OnPropertyEdited();

            Assert.True(FormSchemaDocumentViewModel.IsRelationGroup(Assert.Single(Field("sys_id").Children)));
            Assert.True(_document.IsDirty);
        }

        [Fact]
        [DisplayName("Dragging a mapping after its sibling reorders the field's relation mappings")]
        public void DragMapping_ReordersMappings()
        {
            var customer = (FormField)Field("customer_rowid").Value;
            customer.RelationFieldMappings!.Add(new FieldMapping("sys_id", "sys_id"));
            var group = Field("customer_rowid").Children[0];
            group.Children.Add(new ObjectTreeBuilder().Build(customer.RelationFieldMappings[1]));

            _document.DragDropHandler.Drop(group.Children[0], group.Children[1], TreeNodeDropPosition.After);

            Assert.Equal(["sys_id", "sys_name"], customer.RelationFieldMappings!.Select(m => m.SourceField));
        }
    }
}
