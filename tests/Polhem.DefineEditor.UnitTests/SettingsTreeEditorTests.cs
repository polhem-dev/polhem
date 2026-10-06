using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Database;
using Polhem.Definition.Language;
using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The editors whose tree has a shape worth pinning: the menu's folders, the system settings'
    /// extended properties folder, and the folders of the database settings, table schema and language resource.
    /// </summary>
    public sealed class SettingsTreeEditorTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-settings-trees-" + Guid.NewGuid().ToString("N"));

        public SettingsTreeEditorTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private string Save(object settings, string fileName)
        {
            var path = Path.Combine(_directory, fileName);
            XmlCodec.SerializeToFile(settings, path);
            return path;
        }

        private static IReadOnlyList<TreeNodeCommand> CommandsFor(ObjectTreeDocumentViewModelBase document, ObjectTreeNode node)
        {
            document.SelectedTreeNode = node;
            return document.CommandProvider.GetCommands(node);
        }

        [Fact]
        [DisplayName("Menu folders start expanded and offer Add folder and Add entry, which add under the folder")]
        public void MenuSettings_Folder_ExpandedAndAddsUnderIt()
        {
            var menu = new MenuSettings();
            menu.Items!.AddFolder("hr", "HR").Items!.AddFolder("payroll", "Payroll");
            var document = MenuSettingsDocumentViewModel.Load(Save(menu, "MenuSettings.xml"));
            var hr = document.RootNode.Children[0];
            var payroll = hr.Children[0];

            Assert.True(hr.IsExpanded);
            Assert.True(payroll.IsExpanded);
            var commands = CommandsFor(document, payroll);
            Assert.Equal(
                [LocalizationService.Current["Menu_AddFolder"], LocalizationService.Current["Menu_AddEntry"], LocalizationService.Current["Action_Delete"]],
                commands.Select(c => c.Label));

            commands[1].Execute();

            Assert.IsType<MenuEntry>(Assert.Single(((MenuFolder)payroll.Value).Items!));
            Assert.Same(payroll, document.SelectedTreeNode!.Parent);
        }

        [Fact]
        [DisplayName("System settings end with an ExtendedProperties folder whose Add property adds to the settings and the folder")]
        public void SystemSettings_ExtendedPropertiesFolder_AddsProperty()
        {
            var settings = new SystemSettings();
            settings.ExtendedProperties!.Add(new Property { Name = "Theme", Value = "dark" });
            var document = SystemSettingsDocumentViewModel.Load(Save(settings, "SystemSettings.xml"));

            var folder = document.RootNode.Children[^1];
            Assert.True(folder.IsFolder);
            Assert.Equal("ExtendedProperties", folder.Label);
            Assert.Equal("Theme=dark", Assert.Single(folder.Children).Label);

            Assert.Single(CommandsFor(document, folder)).Execute();

            Assert.Equal(2, document.Root.ExtendedProperties!.Count);
            Assert.Equal(2, folder.Children.Count);
        }

        [Fact]
        [DisplayName("Database settings show expanded Servers and Databases folders, each with its own Add command")]
        public void DatabaseSettings_Folders_OfferTheirAddCommands()
        {
            var document = DatabaseSettingsDocumentViewModel.Load(Save(new DatabaseSettings(), "DatabaseSettings.xml"));

            Assert.Equal(["Servers", "Databases"], document.RootNode.Children.Select(c => c.Label));
            Assert.All(document.RootNode.Children, c => Assert.True(c.IsExpanded));
            Assert.Equal(LocalizationService.Current["DatabaseSettings_AddServer"],
                Assert.Single(CommandsFor(document, document.RootNode.Children[0])).Label);

            Assert.Single(CommandsFor(document, document.RootNode.Children[1])).Execute();

            Assert.Single(document.Root.Items!);
            Assert.Single(document.RootNode.Children[1].Children);
        }

        [Fact]
        [DisplayName("A table schema shows Fields and Indexes folders, and an index lists its fields and offers Add index field")]
        public void TableSchema_Index_ListsFieldsAndAddsOne()
        {
            var schema = new TableSchema { TableName = "ft_order", DisplayName = "Order" };
            schema.Fields!.Add(new DbField("sys_id", "Order No", FieldDbType.String));
            var index = new DbTableIndex { Name = "PK_ft_order", PrimaryKey = true };
            index.IndexFields!.Add(new IndexField("sys_id", SortDirection.Asc));
            schema.Indexes!.Add(index);
            var document = TableSchemaDocumentViewModel.Load(Save(schema, "ft_order.TableSchema.xml"));

            Assert.Equal(["Fields", "Indexes"], document.RootNode.Children.Select(c => c.Label));
            var indexNode = Assert.Single(document.RootNode.Children[1].Children);
            Assert.Equal("sys_id Asc", Assert.Single(indexNode.Children).Label);
            Assert.Equal("IconLock", document.IconKeyFor(indexNode));

            CommandsFor(document, indexNode)[0].Execute();

            Assert.Equal(2, ((DbTableIndex)indexNode.Value).IndexFields!.Count);
            Assert.Equal(2, indexNode.Children.Count);
        }

        [Fact]
        [DisplayName("A language resource shows Items and Enums folders, and an enum offers Add entry")]
        public void Language_Enum_AddsEntry()
        {
            var resource = new LanguageResource { Namespace = "Employee", Lang = "en" };
            resource.Items.Add(new LanguageItem { Key = "Caption", Value = "Employee" });
            var gender = new LanguageEnum { Name = "Gender" };
            resource.Enums.Add(gender);
            var document = LanguageDocumentViewModel.Load(Save(resource, "Employee.Language.xml"));

            Assert.Equal(["Items", "Enums"], document.RootNode.Children.Select(c => c.Label));
            var enumNode = Assert.Single(document.RootNode.Children[1].Children);

            CommandsFor(document, enumNode)[0].Execute();

            Assert.Single(((LanguageEnum)enumNode.Value).Entries);
            Assert.Single(enumNode.Children);
        }
    }
}
