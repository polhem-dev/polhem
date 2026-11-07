using System.ComponentModel;
using System.Globalization;
using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.BrowserPreview;
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
            ((LayoutSection)MainSection.Value).Caption = "Edited";

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
        [DisplayName("The preview starts in Edit mode at full width, drawn by Avalonia, with field names shown")]
        public void PreviewSettings_Defaults()
        {
            Assert.Equal(SingleFormMode.Edit, _document.PreviewFormMode);
            Assert.False(_document.IsPreviewCompact);
            Assert.True(_document.ShowPreviewFieldNames);
            Assert.Equal(FormLayoutPreviewEngine.Avalonia, _document.PreviewEngine);
            Assert.True(_document.IsAvaloniaPreview);
            Assert.False(_document.IsWebPreview);
        }

        [Fact]
        [DisplayName("Turning on the web preview switches PreviewEngine to Web and notifies both properties")]
        public void IsWebPreview_SwitchesPreviewEngine()
        {
            var notified = new List<string?>();
            _document.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

            _document.IsWebPreview = true;

            Assert.Equal(FormLayoutPreviewEngine.Web, _document.PreviewEngine);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.PreviewEngine), notified);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.IsWebPreview), notified);

            _document.IsWebPreview = false;

            Assert.Equal(FormLayoutPreviewEngine.Avalonia, _document.PreviewEngine);
        }

        [Theory]
        [InlineData(FormLayoutPreviewEngine.Web, true)]
        [InlineData(FormLayoutPreviewEngine.Avalonia, false)]
        [DisplayName("Choosing an engine in the preview's switch sets PreviewEngine and notifies which segment is selected")]
        public void SetPreviewEngine_SelectsTheSegment(FormLayoutPreviewEngine engine, bool web)
        {
            // Start from the other engine, so the command really changes it.
            _document.PreviewEngine = web ? FormLayoutPreviewEngine.Avalonia : FormLayoutPreviewEngine.Web;
            var notified = new List<string?>();
            _document.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

            _document.SetPreviewEngineCommand.Execute(engine);

            Assert.Equal(engine, _document.PreviewEngine);
            Assert.Equal(web, _document.IsWebPreview);
            Assert.Equal(!web, _document.IsAvaloniaPreview);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.IsWebPreview), notified);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.IsAvaloniaPreview), notified);
        }

        [Fact]
        [DisplayName("A layout opened with an engine, the one chosen last, starts its preview with that engine")]
        public void Load_WithPreviewEngine_StartsWithIt()
        {
            var document = FormLayoutDocumentViewModel.Load(_document.FilePath, previewEngine: FormLayoutPreviewEngine.Web);

            Assert.Equal(FormLayoutPreviewEngine.Web, document.PreviewEngine);
        }

        [Fact]
        [DisplayName("Publishing to the preview server starts it, stores the unsaved layout and returns the URL that shows it")]
        public async Task PublishBrowserPreviewAsync_StartsTheServerAndPublishesTheLayout()
        {
            await using var host = new BrowserPreviewHost(new BrowserPreviewHub(), static () => CultureInfo.GetCultureInfo("en"));
            _document.Root.Sections![0].Fields!.Add(new LayoutField { FieldName = "remark", Caption = "Remark" });

            var url = await _document.PublishBrowserPreviewAsync(host);

            Assert.True(host.IsRunning);
            var snapshot = host.Hub.GetSnapshot(host.Hub.GetDocumentId(_document.DocumentKey));
            Assert.NotNull(snapshot);
            Assert.Equal(3, XmlCodec.Deserialize<FormLayout>(snapshot.LayoutXml)!.Sections![0].Fields!.Count);
            using var client = new HttpClient();
            var html = await client.GetStringAsync(url);
            Assert.Contains("id=\"preview-remark\"", html, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("?canvas=light", false)]
        [InlineData("?canvas=dark", true)]
        [DisplayName("The preview page draws a dark canvas only when the editor asks for one")]
        public async Task PreviewPage_CanvasQuery_SetsTheCanvas(string query, bool dark)
        {
            await using var host = new BrowserPreviewHost(new BrowserPreviewHub(), static () => CultureInfo.GetCultureInfo("en"));
            var url = await _document.PublishBrowserPreviewAsync(host);

            using var client = new HttpClient();
            var html = await client.GetStringAsync(new Uri(url.AbsoluteUri + query));

            Assert.Contains("browser-preview-canvas", html, StringComparison.Ordinal);
            Assert.Equal(dark, html.Contains("browser-preview-canvas--dark", StringComparison.Ordinal));
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
        [DisplayName("A layout has a preview view, and it starts in Design")]
        public void ViewMode_DefaultsToDesign_AndSupportsPreview()
        {
            Assert.True(_document.SupportsPreview);
            Assert.True(_document.ShowPreviewCommand.CanExecute(null));
            Assert.Equal(DocumentViewMode.Design, _document.ViewMode);
            Assert.False(_document.IsPreviewView);
        }

        [Fact]
        [DisplayName("Show Preview shows the tree beside the preview and notifies the views that follow it")]
        public async Task ShowPreview_ShowsTheTreeBesideThePreview()
        {
            var notified = new List<string?>();
            _document.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

            await _document.ShowPreviewCommand.ExecuteAsync(null);

            Assert.Equal(DocumentViewMode.Preview, _document.ViewMode);
            Assert.True(_document.IsPreviewView);
            Assert.True(_document.IsDesignVisible);
            Assert.False(_document.IsDesignOnly);
            Assert.False(_document.IsXmlView);
            Assert.Contains(nameof(FormLayoutDocumentViewModel.IsPreviewView), notified);
        }

        [Fact]
        [DisplayName("Switching from XML that cannot be read to the preview discards the XML edits when no window can ask")]
        public async Task ShowPreview_FromXmlWithIssues_DiscardsXmlEdits()
        {
            _document.ShowXmlCommand.Execute(null);
            _document.OnXmlTextEdited(_document.XmlText.Replace("</FormLayout>", string.Empty, StringComparison.Ordinal));

            await _document.ShowPreviewCommand.ExecuteAsync(null);

            Assert.Equal(DocumentViewMode.Preview, _document.ViewMode);
            Assert.Empty(_document.XmlIssues);
        }

        [Fact]
        [DisplayName("Show Design from the preview goes back to the tree beside the property grid")]
        public async Task ShowDesign_FromPreview_ShowsTheDesign()
        {
            await _document.ShowPreviewCommand.ExecuteAsync(null);

            await _document.ShowDesignCommand.ExecuteAsync(null);

            Assert.Equal(DocumentViewMode.Design, _document.ViewMode);
            Assert.True(_document.IsDesignOnly);
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
