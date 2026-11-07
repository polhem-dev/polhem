using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The split view of a document (<see cref="ObjectTreeDocumentViewModelBase.IsSplitView"/>): the tree and the XML side
    /// by side, each following the other.
    /// </summary>
    public sealed class XmlSplitViewTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-split-" + Guid.NewGuid().ToString("N"));
        private readonly FormSchemaDocumentViewModel _document;

        public XmlSplitViewTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("total", "Total", FieldDbType.Decimal);
            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            _document = FormSchemaDocumentViewModel.Load(path, SolutionContext.Empty);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private FormField Total => _document.Schema.Tables!["Order"].Fields!["total"];

        [Fact]
        [DisplayName("Split shows the tree and the XML, and Design and XML each show one of them again")]
        public async Task Modes_SwitchBetweenDesignXmlAndSplit()
        {
            _document.ShowSplitCommand.Execute(null);
            Assert.True(_document.IsXmlView);
            Assert.True(_document.IsDesignVisible);
            Assert.False(_document.IsXmlOnly);
            Assert.False(_document.IsDesignOnly);

            _document.ShowXmlCommand.Execute(null);
            Assert.True(_document.IsXmlOnly);
            Assert.False(_document.IsDesignVisible);

            _document.ShowSplitCommand.Execute(null);
            await _document.ShowDesignCommand.ExecuteAsync(null);
            Assert.True(_document.IsDesignOnly);
            Assert.False(_document.IsSplitView);
            Assert.False(_document.IsXmlView);
        }

        [Fact]
        [DisplayName("A document without a preview offers no Preview view")]
        public void ShowPreview_DocumentWithoutPreview_CannotExecute()
        {
            Assert.False(_document.SupportsPreview);
            Assert.False(_document.ShowPreviewCommand.CanExecute(null));
        }

        [Fact]
        [DisplayName("In the split view an edit in the tree shows in the XML at once")]
        public void TreeEdit_InSplit_RefreshesXml()
        {
            _document.ShowSplitCommand.Execute(null);

            Total.Caption = "Grand Total";
            _document.OnPropertyEdited();

            Assert.Contains("Caption=\"Grand Total\"", _document.XmlText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("In the split view the tree takes no edits while the XML cannot be read, and takes them again once it can")]
        public void DesignEnabled_FollowsXmlIssuesInSplit()
        {
            _document.ShowSplitCommand.Execute(null);
            var original = _document.XmlText;

            _document.OnXmlTextEdited(original.Replace("</FormSchema>", string.Empty, StringComparison.Ordinal));
            _document.FlushXmlEdit();
            Assert.False(_document.IsDesignEnabled);

            _document.OnXmlTextEdited(original);
            _document.FlushXmlEdit();
            Assert.True(_document.IsDesignEnabled);
        }

        [Fact]
        [DisplayName("XML that cannot be read leaves the tree editable when the tree is shown on its own")]
        public void DesignEnabled_OutsideSplit_IsTrue()
        {
            _document.ShowXmlCommand.Execute(null);
            _document.OnXmlTextEdited("<FormSchema");
            _document.FlushXmlEdit();

            Assert.True(_document.HasXmlIssues);
            Assert.True(_document.IsDesignEnabled);
        }
    }
}
