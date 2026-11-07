using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// Editing a document as XML: <see cref="ObjectTreeDocumentViewModelBase.FlushXmlEdit"/> reads the typed text back
    /// into the define object, <see cref="DefineXmlParser"/> reports what cannot be read, and Save writes the text as
    /// typed while the document was only edited as XML.
    /// </summary>
    public sealed class XmlEditTests : IDisposable
    {
        private const string Comment = "<!-- Orders placed by customers. -->";

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-xmledit-" + Guid.NewGuid().ToString("N"));
        private readonly string _path;
        private readonly FormSchemaDocumentViewModel _document;

        public XmlEditTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            table.Fields!.Add("total", "Total", FieldDbType.Decimal);
            _path = Path.Combine(_directory, "Order.FormSchema.xml");
            var xml = XmlCodec.Serialize(schema).Replace("<FormSchema ", Comment + "\n<FormSchema ", StringComparison.Ordinal);
            File.WriteAllText(_path, xml);
            _document = FormSchemaDocumentViewModel.Load(_path, SolutionContext.Empty);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private string TotalCaption => _document.Schema.Tables!["Order"].Fields!["total"].Caption;

        private void Type(string text)
        {
            _document.OnXmlTextEdited(text);
            _document.FlushXmlEdit();
        }

        private string EditedXml(string from, string to)
        {
            Assert.Contains(from, _document.XmlText, StringComparison.Ordinal);
            return _document.XmlText.Replace(from, to, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The XML view of an unchanged document shows the file as it is on disk, comments included")]
        public void ShowXml_CleanDocument_ShowsFileText()
        {
            _document.ShowXmlCommand.Execute(null);

            Assert.Equal(File.ReadAllText(_path), _document.XmlText);
            Assert.Contains(Comment, _document.XmlText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Valid XML typed in the XML view is read into the document and the tree, and can be undone")]
        public void FlushXmlEdit_ValidXml_UpdatesObjectAndTree()
        {
            _document.ShowXmlCommand.Execute(null);

            Type(EditedXml("Caption=\"Total\"", "Caption=\"Grand Total\""));

            Assert.Equal("Grand Total", TotalCaption);
            Assert.Empty(_document.XmlIssues);
            Assert.True(_document.IsDirty);
            Assert.Contains(_document.RootNode.Children.SelectMany(c => c.Children).SelectMany(c => c.Children),
                n => n.Value is FormField { Caption: "Grand Total" });

            _document.UndoCommand.Execute(null);
            Assert.Equal("Total", TotalCaption);
        }

        [Fact]
        [DisplayName("A misspelt attribute is reported with its line and leaves the document as it was")]
        public void FlushXmlEdit_UnknownAttribute_ReportsIssueAndKeepsObject()
        {
            _document.ShowXmlCommand.Execute(null);
            var xml = EditedXml("Caption=\"Total\"", "Captoin=\"Grand Total\"");

            Type(xml);

            var issue = Assert.Single(_document.XmlIssues);
            Assert.Equal(XmlParseIssueKind.UnknownAttribute, issue.Kind);
            Assert.Equal("Captoin", issue.Detail);
            Assert.StartsWith("Captoin=", xml.Split('\n')[issue.Line - 1][(issue.Column - 1)..], StringComparison.Ordinal);
            Assert.Equal("Total", TotalCaption);
            Assert.False(_document.CanUndo);
        }

        [Fact]
        [DisplayName("Fixing the XML back to the file clears its problems and the unsaved marker")]
        public void FlushXmlEdit_FixedBackToFile_ClearsIssuesAndDirty()
        {
            _document.ShowXmlCommand.Execute(null);
            var original = _document.XmlText;
            Type(EditedXml("Caption=\"Total\"", "Captoin=\"Total\""));
            Assert.True(_document.HasXmlIssues);

            Type(original);

            Assert.False(_document.HasXmlIssues);
            Assert.False(_document.IsDirty);
            Assert.DoesNotContain("problem", _document.StatusText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("XML that is not well formed is reported and cannot be saved")]
        public async Task Save_MalformedXml_IsBlocked()
        {
            _document.ShowXmlCommand.Execute(null);
            var before = File.ReadAllText(_path);

            Type(EditedXml("</FormSchema>", "</FormSchem>"));
            await _document.SaveCommand.ExecuteAsync(null);

            var issue = Assert.Single(_document.XmlIssues);
            Assert.Equal(XmlParseIssueKind.Syntax, issue.Kind);
            Assert.True(issue.Line > 1);
            Assert.Equal(before, File.ReadAllText(_path));
            Assert.True(_document.IsDirty);
        }

        [Fact]
        [DisplayName("A value that is not of its property's type is reported as a problem")]
        public void FlushXmlEdit_InvalidEnumValue_ReportsIssue()
        {
            _document.ShowXmlCommand.Execute(null);

            Type(EditedXml("DbType=\"Decimal\"", "DbType=\"Money\""));

            Assert.Equal(XmlParseIssueKind.Syntax, Assert.Single(_document.XmlIssues).Kind);
            Assert.Equal(FieldDbType.Decimal, _document.Schema.Tables!["Order"].Fields!["total"].DbType);
        }

        [Fact]
        [DisplayName("Saving a document edited only as XML writes the text as typed, comments included")]
        public async Task Save_AfterXmlEdit_WritesTextAsTyped()
        {
            _document.ShowXmlCommand.Execute(null);
            var xml = EditedXml("Caption=\"Total\"", "Caption=\"Grand Total\"")
                .Replace(Comment, Comment + "\n<!-- Totals include tax. -->", StringComparison.Ordinal);

            Type(xml);
            await _document.SaveCommand.ExecuteAsync(null);

            Assert.Equal(xml, File.ReadAllText(_path));
            Assert.False(_document.IsDirty);
        }

        [Fact]
        [DisplayName("An edit in the tree after an XML edit makes Save write the object again, keeping the file's comments")]
        public async Task Save_AfterTreeEdit_WritesSerializedObject()
        {
            _document.ShowXmlCommand.Execute(null);
            Type(EditedXml("Caption=\"Total\"", "Caption=\"Grand Total\""));
            await _document.ShowDesignCommand.ExecuteAsync(null);

            _document.Schema.DisplayName = "Sales Order";
            _document.OnPropertyEdited();
            await _document.SaveCommand.ExecuteAsync(null);

            var saved = File.ReadAllText(_path);
            Assert.Contains("DisplayName=\"Sales Order\"", saved, StringComparison.Ordinal);
            Assert.Contains(Comment, saved, StringComparison.Ordinal);
            Assert.Equal(XmlCodec.Serialize(_document.Schema), saved.Replace(Comment + "\n", string.Empty, StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A change of comments alone marks the document unsaved, and typing the file back clears it")]
        public void FlushXmlEdit_CommentOnly_TracksDirtyAgainstFile()
        {
            _document.ShowXmlCommand.Execute(null);
            var original = _document.XmlText;

            Type(original.Replace(Comment, "<!-- Changed. -->", StringComparison.Ordinal));
            Assert.True(_document.IsDirty);
            Assert.False(_document.CanUndo);

            Type(original);
            Assert.False(_document.IsDirty);
        }

        [Fact]
        [DisplayName("Switching to Design with XML that cannot be read discards the XML edits when no window can ask")]
        public async Task ShowDesign_WithIssues_DiscardsXmlEdits()
        {
            _document.ShowXmlCommand.Execute(null);
            Type(EditedXml("</FormSchema>", string.Empty));

            await _document.ShowDesignCommand.ExecuteAsync(null);

            Assert.False(_document.IsXmlView);
            Assert.Empty(_document.XmlIssues);
            Assert.Equal(File.ReadAllText(_path), _document.XmlText);
            Assert.Equal("Total", TotalCaption);
        }
    }
}
