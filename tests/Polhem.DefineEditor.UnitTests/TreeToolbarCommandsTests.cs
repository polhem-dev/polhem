using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The toolbar above an editor's tree, as <see cref="TreeCommandProviderBase.GetToolbarCommands"/> builds it and
    /// <see cref="ObjectTreeDocumentViewModelBase"/> exposes it.
    /// </summary>
    public sealed class TreeToolbarCommandsTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-tree-toolbar-" + Guid.NewGuid().ToString("N"));

        public TreeToolbarCommandsTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private static string Text(string key) => LocalizationService.Current[key];

        private static TreeNodeCommand Edit(TreeToolbarCommands commands, string labelKey) =>
            commands.Edit.Single(c => c.Command.Label == Text(labelKey)).Command;

        private string Save<T>(T value, string fileName)
        {
            var path = Path.Combine(_directory, fileName);
            XmlCodec.SerializeToFile(value!, path);
            return path;
        }

        private FormSchemaDocumentViewModel LoadOrderSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            table.Fields!.Add("order_date", "Order Date", FieldDbType.Date);
            return FormSchemaDocumentViewModel.Load(Save(schema, "Order.FormSchema.xml"), SolutionContext.Empty);
        }

        private static ObjectTreeNode TablesFolder(FormSchemaDocumentViewModel document) =>
            document.RootNode.Children.First(c => c.IsFolder && c.Value is FormTableCollection);

        [Fact]
        [DisplayName("A selected field lists Add FormField from its table and Add FormTable from the Tables folder")]
        public void GetToolbarCommands_Field_ListsAncestorAddCommandsNearestFirst()
        {
            var document = LoadOrderSchema();
            var field = TablesFolder(document).Children[0].Children[0];
            document.SelectedTreeNode = field;

            var commands = document.CommandProvider.GetToolbarCommands(field);

            Assert.Equal([Text("FormSchema_AddField"), Text("FormSchema_AddTable")], commands.Add.Select(c => c.Label));
            Assert.All(commands.Add, c => Assert.True(c.IsEnabled));
            Assert.True(Edit(commands, "Action_Delete").IsEnabled);
            Assert.Empty(commands.Actions);
        }

        [Fact]
        [DisplayName("The FormSchema root keeps its form commands in the overflow menu, and its Delete is disabled")]
        public void GetToolbarCommands_FormSchemaRoot_PutsFormCommandsInActions()
        {
            var document = LoadOrderSchema();
            document.SelectedTreeNode = document.RootNode;

            var commands = document.CommandProvider.GetToolbarCommands(document.RootNode);

            Assert.Empty(commands.Add);
            Assert.False(Edit(commands, "Action_Delete").IsEnabled);
            Assert.Equal(
                [
                    Text("FormSchema_GenerateFormLayout"), Text("FormSchema_ExportWorkbook"), Text("FormSchema_SyncDefinitions"),
                    Text("FormSchema_UpgradeTables"), Text("FormSchema_PreviewUpgradeTables"),
                ],
                commands.Actions.Select(c => c.Label));
        }

        [Fact]
        [DisplayName("The FormSchema root's context menu still lists the form commands without Delete")]
        public void GetCommands_FormSchemaRoot_IsUnchanged()
        {
            var document = LoadOrderSchema();
            document.SelectedTreeNode = document.RootNode;

            var commands = document.CommandProvider.GetCommands(document.RootNode);

            Assert.Equal(
                [
                    Text("FormSchema_GenerateFormLayout"), Text("FormSchema_ExportWorkbook"), Text("FormSchema_SyncDefinitions"),
                    Text("FormSchema_UpgradeTables"), Text("FormSchema_PreviewUpgradeTables"),
                ],
                commands.Select(c => c.Label));
        }

        [Fact]
        [DisplayName("Selecting another node refreshes the toolbar items of the document")]
        public void SelectedTreeNode_Changed_RefreshesToolbarItems()
        {
            var document = LoadOrderSchema();
            var tablesFolder = TablesFolder(document);

            document.SelectedTreeNode = tablesFolder;
            Assert.Equal([Text("FormSchema_AddTable")], document.ToolbarAddItems.Select(i => i.Label));
            Assert.False(document.ToolbarEditItems.Single(i => i.Label == Text("Action_Delete")).IsEnabled);

            document.SelectedTreeNode = tablesFolder.Children[0];
            Assert.Equal([Text("FormSchema_AddField"), Text("FormSchema_AddTable")], document.ToolbarAddItems.Select(i => i.Label));
            Assert.True(document.ToolbarEditItems.Single(i => i.Label == Text("Action_Delete")).IsEnabled);
            Assert.False(document.HasToolbarActions);
        }

        [Fact]
        [DisplayName("Running a toolbar Add item adds the object and selects its node")]
        public void ToolbarAddItem_Execute_AddsAndSelects()
        {
            var document = LoadOrderSchema();
            var table = TablesFolder(document).Children[0];
            document.SelectedTreeNode = table.Children[1];

            document.ToolbarAddItems[0].Command.Execute(null);

            var formTable = Assert.IsType<FormTable>(table.Value);
            Assert.Equal(3, formTable.Fields!.Count);
            Assert.Same(formTable.Fields[2], document.SelectedTreeNode!.Value);
            Assert.True(document.IsDirty);
        }

        [Fact]
        [DisplayName("A selected program offers Add program, which adds to the program list")]
        public void ProgramSettings_SelectedProgram_AddsProgram()
        {
            var settings = new ProgramSettings();
            settings.Items!.Add(new ProgramItem("Employee", "Employees"));
            var document = ProgramSettingsDocumentViewModel.Load(Save(settings, "ProgramSettings.xml"));
            document.SelectedTreeNode = document.RootNode.Children[0];

            var add = Assert.Single(document.ToolbarAddItems);
            Assert.Equal(Text("Program_AddProgram"), add.Label);
            add.Command.Execute(null);

            var root = Assert.IsType<ProgramSettings>(document.RootNode.Value);
            Assert.Equal(2, root.Items!.Count);
            Assert.Equal(2, document.RootNode.Children.Count);
        }

        [Fact]
        [DisplayName("A selected menu entry offers Add folder and Add entry, which add next to it in its folder")]
        public void MenuSettings_SelectedEntry_AddsIntoItsFolder()
        {
            var menu = new MenuSettings();
            menu.Items!.AddFolder("hr", "HR").Items!.Add(new MenuEntry("employees", "Employee", "Employees"));
            var document = MenuSettingsDocumentViewModel.Load(Save(menu, "MenuSettings.xml"));
            var hrNode = document.RootNode.Children[0];
            document.SelectedTreeNode = hrNode.Children[0];

            Assert.Equal([Text("Menu_AddFolder"), Text("Menu_AddEntry")], document.ToolbarAddItems.Select(i => i.Label));
            document.ToolbarAddItems[1].Command.Execute(null);

            var folder = Assert.IsType<MenuFolder>(hrNode.Value);
            Assert.Equal(2, folder.Items!.Count);
            Assert.Equal(2, hrNode.Children.Count);
            Assert.Single(Assert.IsType<MenuSettings>(document.RootNode.Value).Items!);
        }

        [Fact]
        [DisplayName("A database server keeps Paste connection string in the overflow menu, not among the Add buttons")]
        public void DatabaseSettings_Server_PasteIsAnAction()
        {
            var settings = new DatabaseSettings();
            settings.Servers!.Add(new DatabaseServer { Id = "main", DatabaseType = DatabaseType.SQLServer });
            var document = DatabaseSettingsDocumentViewModel.Load(Save(settings, "DatabaseSettings.xml"));
            var server = document.RootNode.Children.First(c => c.IsFolder && c.Value is DatabaseServerCollection).Children[0];
            document.SelectedTreeNode = server;

            var commands = document.CommandProvider.GetToolbarCommands(server);

            Assert.DoesNotContain(commands.Add, c => c.Label == Text("DatabaseSettings_PasteConnectionString"));
            Assert.Equal([Text("DatabaseSettings_PasteConnectionString")], commands.Actions.Select(c => c.Label));
            Assert.Equal(
                [Text("DatabaseSettings_PasteConnectionString"), Text("Action_Duplicate"), Text("Action_Delete")],
                document.CommandProvider.GetCommands(server).Select(c => c.Label));
        }
    }
}
