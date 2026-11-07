using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// Duplicate, Move Up and Move Down on the tree of an editor, as <see cref="ObjectTreeDocumentViewModelBase"/>
    /// runs them and the context menu and the toolbar offer them.
    /// </summary>
    public sealed class TreeArrangeCommandsTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-tree-arrange-" + Guid.NewGuid().ToString("N"));
        private readonly FormSchemaDocumentViewModel _document;

        public TreeArrangeCommandsTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            var customer = table.Fields!.Add("customer_rowid", "Customer", FieldDbType.Guid);
            customer.RelationProgId = "Customer";
            customer.RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_customer_name"));
            table.Fields!.Add("ref_customer_name", "Customer Name", FieldDbType.String);
            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            _document = FormSchemaDocumentViewModel.Load(path, SolutionContext.Empty);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private static string Text(string key) => LocalizationService.Current[key];

        private ObjectTreeNode TableNode =>
            _document.RootNode.Children.First(c => c.IsFolder && c.Value is FormTableCollection).Children[0];

        private FormTable Table => (FormTable)TableNode.Value;

        private ObjectTreeNode FieldNode(string fieldName) =>
            TableNode.Children.First(c => c.Value is FormField f && f.FieldName == fieldName);

        private string[] TreeFieldOrder() =>
            TableNode.Children.Select(c => c.Value).OfType<FormField>().Select(f => f.FieldName).ToArray();

        private string[] SchemaFieldOrder() => Table.Fields!.Select(f => f.FieldName).ToArray();

        [Fact]
        [DisplayName("Duplicate inserts a copy with a free _copy key right after the field and selects it")]
        public void Duplicate_Field_InsertsKeyedCopyAfterOriginal()
        {
            _document.SelectedTreeNode = FieldNode("sys_id");

            _document.DuplicateCommand.Execute(null);

            Assert.Equal(["sys_id", "sys_id_copy", "customer_rowid", "ref_customer_name"], SchemaFieldOrder());
            Assert.Equal(SchemaFieldOrder(), TreeFieldOrder());
            var copy = Assert.IsType<FormField>(_document.SelectedTreeNode!.Value);
            Assert.Equal("sys_id_copy", copy.FieldName);
            Assert.Equal("Order No", copy.Caption);
            Assert.True(_document.IsDirty);
        }

        [Fact]
        [DisplayName("A second Duplicate of the same field numbers the key instead of repeating it")]
        public void Duplicate_Twice_NumbersTheKey()
        {
            var original = FieldNode("sys_id");
            _document.SelectedTreeNode = original;
            _document.DuplicateCommand.Execute(null);
            _document.SelectedTreeNode = original;

            _document.DuplicateCommand.Execute(null);

            Assert.Equal(["sys_id", "sys_id_copy2", "sys_id_copy", "customer_rowid", "ref_customer_name"], SchemaFieldOrder());
        }

        [Fact]
        [DisplayName("Duplicate copies the objects a field holds, such as its relation mappings, as new objects")]
        public void Duplicate_FieldWithMappings_CopiesMappings()
        {
            var original = (FormField)FieldNode("customer_rowid").Value;
            _document.SelectedTreeNode = FieldNode("customer_rowid");

            _document.DuplicateCommand.Execute(null);

            var copy = Assert.IsType<FormField>(_document.SelectedTreeNode!.Value);
            var mapping = Assert.Single(copy.RelationFieldMappings!);
            Assert.Equal("ref_customer_name", mapping.DestinationField);
            Assert.NotSame(original.RelationFieldMappings![0], mapping);
            Assert.Equal("Customer", copy.RelationProgId);
        }

        [Fact]
        [DisplayName("The root and a folder cannot be duplicated or moved")]
        public void Commands_RootAndFolder_AreDisabled()
        {
            foreach (var node in new[] { _document.RootNode, _document.RootNode.Children.First(c => c.IsFolder) })
            {
                _document.SelectedTreeNode = node;
                Assert.False(_document.DuplicateCommand.CanExecute(null));
                Assert.False(_document.MoveUpCommand.CanExecute(null));
                Assert.False(_document.MoveDownCommand.CanExecute(null));
            }
        }

        [Fact]
        [DisplayName("The first field cannot move up and the last cannot move down")]
        public void Move_AtTheEnds_IsDisabled()
        {
            _document.SelectedTreeNode = FieldNode("sys_id");
            Assert.False(_document.MoveUpCommand.CanExecute(null));
            Assert.True(_document.MoveDownCommand.CanExecute(null));

            _document.SelectedTreeNode = FieldNode("ref_customer_name");
            Assert.True(_document.MoveUpCommand.CanExecute(null));
            Assert.False(_document.MoveDownCommand.CanExecute(null));
        }

        [Fact]
        [DisplayName("Move Down and Move Up reorder the schema and the tree together and keep the field selected")]
        public void Move_DownThenUp_ReordersSchemaAndTree()
        {
            var node = FieldNode("sys_id");
            _document.SelectedTreeNode = node;

            _document.MoveDownCommand.Execute(null);
            Assert.Equal(["customer_rowid", "sys_id", "ref_customer_name"], SchemaFieldOrder());
            Assert.Equal(SchemaFieldOrder(), TreeFieldOrder());
            Assert.Same(node, _document.SelectedTreeNode);
            Assert.True(_document.IsDirty);

            _document.MoveUpCommand.Execute(null);
            Assert.Equal(["sys_id", "customer_rowid", "ref_customer_name"], SchemaFieldOrder());
            Assert.Equal(SchemaFieldOrder(), TreeFieldOrder());
            Assert.False(_document.MoveUpCommand.CanExecute(null));
        }

        [Fact]
        [DisplayName("A middle field's context menu groups Move Up, Move Down, Duplicate and Delete after its Add commands")]
        public void GetCommands_MiddleField_GroupsEditCommands()
        {
            var node = FieldNode("customer_rowid");
            _document.SelectedTreeNode = node;

            var commands = _document.CommandProvider.GetCommands(node);

            var labels = commands.Select(c => c.Label).ToList();
            var first = labels.IndexOf(Text("Action_MoveUp"));
            Assert.Equal(
                [Text("Action_MoveUp"), Text("Action_MoveDown"), Text("Action_Duplicate"), Text("Action_Delete")],
                labels.Skip(first).Take(4));
            Assert.True(commands[first].BeginsGroup);
        }

        [Fact]
        [DisplayName("Duplicating a menu folder gives the copy and everything in it ids no menu node uses")]
        public void Duplicate_MenuFolder_RenamesEveryIdInTheCopy()
        {
            var menu = new MenuSettings();
            var hr = menu.Items!.AddFolder("hr", "HR");
            hr.Items!.Add(new MenuEntry("employees", "Employee", "Employees"));
            var path = Path.Combine(_directory, "MenuSettings.xml");
            XmlCodec.SerializeToFile(menu, path);
            var document = MenuSettingsDocumentViewModel.Load(path);
            document.SelectedTreeNode = document.RootNode.Children[0];

            document.DuplicateCommand.Execute(null);

            var ids = document.Root.EnumerateNodes().Select(n => n.Id).ToList();
            Assert.Equal(["hr", "employees", "hr_copy", "employees_copy"], ids);
            Assert.Empty(document.Root.Validate());
        }

        [Fact]
        [DisplayName("The toolbar shows every edit command with its key in the tooltip, disabled where it cannot run")]
        public void ToolbarEditItems_FirstField_ShowShortcutsAndState()
        {
            _document.SelectedTreeNode = FieldNode("sys_id");

            var items = _document.ToolbarEditItems;

            Assert.Equal(
                [Text("Action_MoveUp"), Text("Action_MoveDown"), Text("Action_Duplicate"), Text("Action_Delete")],
                items.Select(i => i.Label));
            Assert.Equal([false, true, true, true], items.Select(i => i.IsEnabled));
            Assert.Equal($"{Text("Action_Duplicate")} ({TreeShortcuts.Duplicate})", items[2].ToolTip);
        }
    }
}
