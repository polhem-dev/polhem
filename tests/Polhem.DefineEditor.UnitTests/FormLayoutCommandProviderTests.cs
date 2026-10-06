using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The context menu of the FormLayout tree, as <see cref="FormLayoutCommandProvider"/> builds it.
    /// </summary>
    public sealed class FormLayoutCommandProviderTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-formlayout-commands-" + Guid.NewGuid().ToString("N"));
        private readonly FormLayoutDocumentViewModel _document;

        public FormLayoutCommandProviderTests()
        {
            Directory.CreateDirectory(_directory);
            var layout = new FormLayout { LayoutId = "Employee", ProgId = "Employee", Caption = "Employee" };
            var section = new LayoutSection { Name = "Main", Caption = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "Id" });
            layout.Sections!.Add(section);
            var path = Path.Combine(_directory, "Employee.FormLayout.xml");
            XmlCodec.SerializeToFile(layout, path);
            _document = FormLayoutDocumentViewModel.Load(path);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private ObjectTreeNode SectionsFolder => _document.RootNode.Children.First(c => c.IsFolder && c.Value is LayoutSectionCollection);

        private IReadOnlyList<TreeNodeCommand> CommandsFor(ObjectTreeNode node)
        {
            // The commands act on the selected node, which the tree selects on right-click.
            _document.SelectedTreeNode = node;
            return _document.CommandProvider.GetCommands(node);
        }

        [Fact]
        [DisplayName("The root offers no commands")]
        public void GetCommands_Root_IsEmpty()
        {
            Assert.Empty(CommandsFor(_document.RootNode));
        }

        [Fact]
        [DisplayName("The Sections folder offers Add section and no Delete")]
        public void GetCommands_SectionsFolder_OffersAddSection()
        {
            var command = Assert.Single(CommandsFor(SectionsFolder));

            Assert.Equal(LocalizationService.Current["FormLayout_AddSection"], command.Label);
            Assert.Equal("IconSection", command.IconKey);
        }

        [Fact]
        [DisplayName("A section offers Add field, then Delete in a group of its own")]
        public void GetCommands_Section_OffersAddFieldAndDelete()
        {
            var commands = CommandsFor(SectionsFolder.Children[0]);

            Assert.Equal(
                [LocalizationService.Current["FormLayout_AddField"], LocalizationService.Current["Action_Delete"]],
                commands.Select(c => c.Label));
            Assert.True(commands[1].BeginsGroup);
        }

        [Fact]
        [DisplayName("Running Add section adds a section to the layout and a node to the Sections folder")]
        public void Execute_AddSection_AddsSectionAndNode()
        {
            var folder = SectionsFolder;

            Assert.Single(CommandsFor(folder)).Execute();

            Assert.Equal(2, _document.Root.Sections!.Count);
            Assert.Equal(2, folder.Children.Count);
            Assert.Same(folder.Children[1], _document.SelectedTreeNode);
        }
    }
}
