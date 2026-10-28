using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// What the FormLayout editor's live preview listens to: <see cref="ObjectTreeDocumentViewModelBase.DocumentChanged"/>
    /// on every edit, and the preview settings of <see cref="FormLayoutDocumentViewModel"/>.
    /// </summary>
    public sealed class FormLayoutPreviewStateTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-formlayout-preview-" + Guid.NewGuid().ToString("N"));
        private readonly FormLayoutDocumentViewModel _document;
        private int _changes;

        public FormLayoutPreviewStateTests()
        {
            Directory.CreateDirectory(_directory);
            var layout = new FormLayout { LayoutId = "Employee", ProgId = "Employee", Caption = "Employee" };
            var section = new LayoutSection { Name = "Main", Caption = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "Id" });
            section.Fields.Add(new LayoutField { FieldName = "sys_name", Caption = "Name" });
            layout.Sections!.Add(section);
            var path = Path.Combine(_directory, "Employee.FormLayout.xml");
            XmlCodec.SerializeToFile(layout, path);
            _document = FormLayoutDocumentViewModel.Load(path);
            _document.DocumentChanged += (_, _) => _changes++;
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private ObjectTreeNode SectionsFolder => _document.RootNode.Children.First(c => c.IsFolder && c.Value is LayoutSectionCollection);

        private ObjectTreeNode MainSection => SectionsFolder.Children[0];

        [Fact]
        [DisplayName("Adding a node raises DocumentChanged each time, also while the document is already dirty")]
        public void AddNode_RaisesDocumentChangedEveryTime()
        {
            _document.SelectedTreeNode = SectionsFolder;

            _document.AddSectionCommand.Execute(null);
            Assert.True(_document.IsDirty);
            _document.AddSectionCommand.Execute(null);

            Assert.Equal(2, _changes);
        }

        [Fact]
        [DisplayName("A property written in the grid raises DocumentChanged")]
        public void OnPropertyEdited_RaisesDocumentChanged()
        {
            _document.SelectedTreeNode = MainSection;

            _document.OnPropertyEdited();

            Assert.Equal(1, _changes);
            Assert.True(_document.IsDirty);
        }

        [Fact]
        [DisplayName("Reordering nodes by drag and drop raises DocumentChanged")]
        public void Drop_RaisesDocumentChanged()
        {
            var first = MainSection.Children[0];
            var last = MainSection.Children[1];

            _document.DragDropHandler.Drop(first, last, TreeNodeDropPosition.After);

            Assert.Equal(1, _changes);
        }

        [Fact]
        [DisplayName("The preview starts expanded in Edit mode at full width, with field names shown")]
        public void PreviewSettings_Defaults()
        {
            Assert.True(_document.IsPreviewVisible);
            Assert.Equal(SingleFormMode.Edit, _document.PreviewFormMode);
            Assert.False(_document.IsPreviewCompact);
            Assert.True(_document.ShowPreviewFieldNames);
        }

        [Fact]
        [DisplayName("Turning on View mode switches PreviewFormMode and notifies it")]
        public void IsPreviewViewMode_SwitchesPreviewFormMode()
        {
            var notified = new List<string?>();
            _document.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

            _document.IsPreviewViewMode = true;

            Assert.Equal(SingleFormMode.View, _document.PreviewFormMode);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.PreviewFormMode), notified);
        }
            [Fact]
        [DisplayName("The side starts with the property grid above the preview")]
        public void PaneLayout_DefaultsToPropertiesAndPreview()
        {
            Assert.Equal(FormLayoutPaneLayout.PropertiesAndPreview, _document.PaneLayout);
            Assert.True(_document.IsPropertyPaneVisible);
            Assert.True(_document.IsPreviewPaneVisible);
        }

        [Theory]
        [InlineData(FormLayoutPaneLayout.PreviewOnly, false, true)]
        [InlineData(FormLayoutPaneLayout.PropertiesOnly, true, false)]
        [InlineData(FormLayoutPaneLayout.PropertiesAndPreview, true, true)]
        [DisplayName("Choosing a side layout shows the panes it names and notifies their visibility")]
        public void SetPaneLayout_ShowsTheNamedPanes(FormLayoutPaneLayout layout, bool properties, bool preview)
        {
            // Start from another layout, so the command really changes it.
            _document.PaneLayout = layout == FormLayoutPaneLayout.PropertiesOnly
                ? FormLayoutPaneLayout.PreviewOnly
                : FormLayoutPaneLayout.PropertiesOnly;
            var notified = new List<string?>();
            _document.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

            _document.SetPaneLayoutCommand.Execute(layout);

            Assert.Equal(layout, _document.PaneLayout);
            Assert.Equal(properties, _document.IsPropertyPaneVisible);
            Assert.Equal(preview, _document.IsPreviewPaneVisible);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.IsPropertyPaneVisible), notified);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.IsPreviewPaneVisible), notified);
        }

        [Fact]
        [DisplayName("Showing only the preview expands a collapsed preview")]
        public void SetPaneLayout_PreviewOnly_ExpandsThePreview()
        {
            _document.IsPreviewVisible = false;

            _document.SetPaneLayoutCommand.Execute(FormLayoutPaneLayout.PreviewOnly);

            Assert.True(_document.IsPreviewVisible);
        }

        [Fact]
        [DisplayName("Properties can be edited in a dialog for a node, not for a folder")]
        public void EditPropertiesCommand_FollowsTheSelectedNode()
        {
            var changed = 0;
            _document.EditPropertiesCommand.CanExecuteChanged += (_, _) => changed++;

            _document.SelectedTreeNode = SectionsFolder;
            Assert.False(_document.EditPropertiesCommand.CanExecute(null));

            _document.SelectedTreeNode = MainSection;
            Assert.True(_document.EditPropertiesCommand.CanExecute(null));
            Assert.Equal(2, changed);
        }

        [Fact]
        [DisplayName("The browser preview snapshot keeps the layout as it was, whatever is edited afterwards")]
        public void CreateBrowserPreviewSnapshot_LaterEdit_DoesNotChangeTheSnapshot()
        {
            var snapshot = _document.CreateBrowserPreviewSnapshot();

            _document.Root.Sections![0].Fields!.Add(new LayoutField { FieldName = "remark", Caption = "Remark" });

            var layout = XmlCodec.Deserialize<FormLayout>(snapshot.LayoutXml)!;
            Assert.Equal(2, layout.Sections![0].Fields!.Count);
            Assert.Null(snapshot.Error);
        }

        [Fact]
        [DisplayName("The browser preview snapshot carries the preview settings and the layout caption")]
        public void CreateBrowserPreviewSnapshot_CarriesPreviewSettings()
        {
            _document.IsPreviewViewMode = true;
            _document.IsPreviewCompact = true;
            _document.ShowPreviewFieldNames = false;

            var snapshot = _document.CreateBrowserPreviewSnapshot();

            Assert.Equal("Employee", snapshot.Title);
            Assert.Equal(SingleFormMode.View, snapshot.FormMode);
            Assert.True(snapshot.IsCompact);
            Assert.False(snapshot.ShowFieldNames);
        }
    }
}
