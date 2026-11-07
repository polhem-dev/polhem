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
    /// The XML view of a document (<see cref="ObjectTreeDocumentViewModelBase.IsXmlView"/>) and
    /// <see cref="XmlElementLocator"/>, which puts the caret on the selected node's element.
    /// </summary>
    public sealed class XmlViewTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-xmlview-" + Guid.NewGuid().ToString("N"));
        private readonly FormSchemaDocumentViewModel _document;

        public XmlViewTests()
        {
            Directory.CreateDirectory(_directory);
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            master.Fields!.Add("total", "Total", FieldDbType.Decimal);
            var detail = schema.Tables!.Add("OrderLine", "Order Line");
            detail.Fields!.Add("sys_id", "Line No", FieldDbType.String);
            detail.Fields!.Add("quantity", "Quantity", FieldDbType.Decimal);
            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            _document = FormSchemaDocumentViewModel.Load(path, SolutionContext.Empty);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private ObjectTreeNode TablesFolder =>
            _document.RootNode.Children.First(c => c.IsFolder && c.Value is FormTableCollection);

        private ObjectTreeNode FieldNode(string tableName, string fieldName) =>
            TablesFolder.Children.First(t => t.Value is FormTable table && table.TableName == tableName)
                .Children.First(c => c.Value is FormField f && f.FieldName == fieldName);

        private static string LineAt(string xml, XmlTextPosition position) =>
            xml.Split('\n')[position.Line - 1];

        private static string NameAt(string xml, XmlTextPosition position) =>
            LineAt(xml, position).Substring(position.Column - 1, position.Length);

        [Fact]
        [DisplayName("Opening the XML view shows the document as a save would write it, unsaved edits included")]
        public void ShowXml_ShowsSerializedObjectWithUnsavedEdits()
        {
            _document.SelectedTreeNode = FieldNode("Order", "total");
            ((FormField)_document.SelectedTreeNode.Value).Caption = "Grand Total";
            _document.OnPropertyEdited();

            _document.ShowXmlCommand.Execute(null);

            Assert.True(_document.IsXmlView);
            Assert.Equal(XmlCodec.Serialize(_document.Schema), _document.XmlText);
            Assert.Contains("Grand Total", _document.XmlText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The XML follows an undo made while the XML view is open")]
        public void Undo_WhileXmlViewOpen_RefreshesXmlText()
        {
            _document.SelectedTreeNode = FieldNode("Order", "total");
            ((FormField)_document.SelectedTreeNode.Value).Caption = "Grand Total";
            _document.OnPropertyEdited();
            _document.ShowXmlCommand.Execute(null);

            _document.UndoCommand.Execute(null);

            Assert.DoesNotContain("Grand Total", _document.XmlText, StringComparison.Ordinal);
            Assert.Equal(XmlCodec.Serialize(_document.Schema), _document.XmlText);
        }

        [Fact]
        [DisplayName("The XML view finds the element of the selected field, not a field of the same name in another table")]
        public void LocateSelectedNode_Field_FindsItsElement()
        {
            _document.SelectedTreeNode = FieldNode("OrderLine", "sys_id");
            _document.ShowXmlCommand.Execute(null);

            var position = _document.LocateSelectedNodeInXml();

            Assert.NotNull(position);
            Assert.Equal("FormField", NameAt(_document.XmlText, position));
            Assert.Contains("Line No", LineAt(_document.XmlText, position), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The XML view finds the element of a collection folder")]
        public void LocateSelectedNode_Folder_FindsCollectionElement()
        {
            _document.SelectedTreeNode = TablesFolder;
            _document.ShowXmlCommand.Execute(null);

            var position = _document.LocateSelectedNodeInXml();

            Assert.NotNull(position);
            Assert.Equal("Tables", NameAt(_document.XmlText, position));
        }

        [Fact]
        [DisplayName("XmlElementLocator finds an item of a collection written as repeated elements")]
        public void Find_FlattenedCollectionItem_FindsItsElement()
        {
            var resource = new LanguageResource();
            var gender = new LanguageEnum { Name = "Gender" };
            resource.Enums.Add(gender);
            gender.Entries.Add("M", "Male");
            var female = gender.Entries.Add("F", "Female");
            var xml = XmlCodec.Serialize(resource);

            var position = XmlElementLocator.Find(xml, resource, female);

            Assert.NotNull(position);
            Assert.Equal("Entry", NameAt(xml, position));
            Assert.Contains("Female", LineAt(xml, position), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("XmlElementLocator returns null for an object that is not part of the definition")]
        public void Find_ObjectOutsideDefinition_ReturnsNull()
        {
            var xml = XmlCodec.Serialize(_document.Schema);

            Assert.Null(XmlElementLocator.Find(xml, _document.Schema, new FormField()));
        }
    }
}
