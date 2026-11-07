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
    /// <see cref="XmlCommentCarrier"/> puts a definition file's comments back into the XML the serializer writes, and a
    /// save from the tree keeps them.
    /// </summary>
    public sealed class XmlCommentCarrierTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-comments-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        private static FormSchema Order()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "Order");
            table.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            table.Fields!.Add("total", "Total", FieldDbType.Decimal);
            table.Fields!.Add("status", "Status", FieldDbType.String);
            return schema;
        }

        // The serializer's XML with a comment line put before the first line that contains `before`.
        private static string WithComment(string xml, string before, string comment)
        {
            var lines = xml.Split('\n').ToList();
            var index = lines.FindIndex(l => l.Contains(before, StringComparison.Ordinal));
            var indent = lines[index][..(lines[index].Length - lines[index].TrimStart().Length)];
            lines.Insert(index, indent + comment);
            return string.Join('\n', lines);
        }

        private static int LineOf(string xml, string text) =>
            xml.Split('\n').ToList().FindIndex(l => l.Contains(text, StringComparison.Ordinal));

        [Fact]
        [DisplayName("XML without comments is written exactly as the serializer writes it")]
        public void Carry_NoComments_ReturnsGenerated()
        {
            var xml = XmlCodec.Serialize(Order());

            Assert.Equal(xml, XmlCommentCarrier.Carry(xml, xml));
        }

        [Fact]
        [DisplayName("A comment stays before its field when the field moves")]
        public void Carry_FieldMoved_CommentFollowsField()
        {
            var schema = Order();
            var original = WithComment(XmlCodec.Serialize(schema), "FieldName=\"total\"", "<!-- Includes tax. -->");
            var fields = schema.Tables!["Order"].Fields!;
            var total = fields["total"];
            fields.Remove(total);
            fields.Insert(0, total);

            var carried = XmlCommentCarrier.Carry(original, XmlCodec.Serialize(schema));

            Assert.Equal(LineOf(carried, "FieldName=\"total\"") - 1, LineOf(carried, "<!-- Includes tax. -->"));
            Assert.True(LineOf(carried, "FieldName=\"total\"") < LineOf(carried, "FieldName=\"sys_id\""));
        }

        [Fact]
        [DisplayName("A comment stays with its field when a field is added before it")]
        public void Carry_FieldAddedBefore_CommentFollowsField()
        {
            var schema = Order();
            var original = WithComment(XmlCodec.Serialize(schema), "FieldName=\"status\"", "<!-- Draft or Confirmed. -->");
            schema.Tables!["Order"].Fields!.Insert(0, new FormField("note", "Note", FieldDbType.String));

            var carried = XmlCommentCarrier.Carry(original, XmlCodec.Serialize(schema));

            Assert.Equal(LineOf(carried, "FieldName=\"status\"") - 1, LineOf(carried, "<!-- Draft or Confirmed. -->"));
        }

        [Fact]
        [DisplayName("A comment before the root element, over several lines, stays before it as it was")]
        public void Carry_HeaderComment_KeptBeforeRoot()
        {
            var schema = Order();
            const string Header = "<!--\n  Orders placed by customers.\n  Totals include tax.\n-->";
            var original = WithComment(XmlCodec.Serialize(schema), "<FormSchema", Header);
            schema.DisplayName = "Sales Order";

            var carried = XmlCommentCarrier.Carry(original, XmlCodec.Serialize(schema));

            Assert.Contains(Header + "\n<FormSchema", carried, StringComparison.Ordinal);
            Assert.StartsWith("<?xml", carried, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A comment whose field was deleted moves to the end of the element that held it")]
        public void Carry_FieldDeleted_CommentMovesToParentEnd()
        {
            var schema = Order();
            var original = WithComment(XmlCodec.Serialize(schema), "FieldName=\"total\"", "<!-- Includes tax. -->");
            schema.Tables!["Order"].Fields!.Remove("total");

            var carried = XmlCommentCarrier.Carry(original, XmlCodec.Serialize(schema));

            Assert.Equal(LineOf(carried, "</Fields>") - 1, LineOf(carried, "<!-- Includes tax. -->"));
        }

        [Fact]
        [DisplayName("A comment after the last field stays at the end of its fields")]
        public void Carry_TrailingComment_StaysAtEnd()
        {
            var schema = Order();
            var original = WithComment(XmlCodec.Serialize(schema), "</Fields>", "  <!-- More fields come with the next release. -->");
            schema.Tables!["Order"].Fields!.Add("note", "Note", FieldDbType.String);

            var carried = XmlCommentCarrier.Carry(original, XmlCodec.Serialize(schema));

            var comment = LineOf(carried, "More fields come");
            Assert.Equal(LineOf(carried, "</Fields>") - 1, comment);
            Assert.True(LineOf(carried, "FieldName=\"note\"") < comment);
        }

        [Fact]
        [DisplayName("Saving from the tree keeps the comments of the file")]
        public async Task Save_TreeEdit_KeepsComments()
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            File.WriteAllText(path, WithComment(XmlCodec.Serialize(Order()), "FieldName=\"total\"", "<!-- Includes tax. -->") + "\n");
            var document = FormSchemaDocumentViewModel.Load(path, SolutionContext.Empty);

            document.Schema.Tables!["Order"].Fields!["total"].Caption = "Grand Total";
            document.OnPropertyEdited();
            document.ShowXmlCommand.Execute(null);
            var shown = document.XmlText;
            await document.SaveCommand.ExecuteAsync(null);

            var saved = File.ReadAllText(path);
            Assert.Equal(shown, saved);
            Assert.EndsWith("</FormSchema>\n", saved, StringComparison.Ordinal);
            Assert.Equal(LineOf(saved, "Caption=\"Grand Total\"") - 1, LineOf(saved, "<!-- Includes tax. -->"));
        }
    }
}
