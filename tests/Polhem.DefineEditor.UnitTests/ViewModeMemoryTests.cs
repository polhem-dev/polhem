using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// <see cref="ViewModeMemory"/>: each kind of define document opens in the view the last one of its kind was left in.
    /// </summary>
    public sealed class ViewModeMemoryTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-viewmode-" + Guid.NewGuid().ToString("N"));

        public ViewModeMemoryTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private FormLayoutDocumentViewModel LoadLayout()
        {
            var path = Path.Combine(_directory, "Order.FormLayout.xml");
            XmlCodec.SerializeToFile(new FormLayout { LayoutId = "Order", ProgId = "Order", Caption = "Order" }, path);
            return FormLayoutDocumentViewModel.Load(path);
        }

        private FormSchemaDocumentViewModel LoadSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            schema.Tables!.Add("Order", "Order").Fields!.Add("total", "Total", FieldDbType.Decimal);
            var path = Path.Combine(_directory, "Order.FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            return FormSchemaDocumentViewModel.Load(path, SolutionContext.Empty);
        }

        [Theory]
        [InlineData(DocumentViewMode.Preview)]
        [InlineData(DocumentViewMode.Xml)]
        [InlineData(DocumentViewMode.Split)]
        [DisplayName("A layout opens in the view the previous layout was left in")]
        public void Restore_AfterRemember_OpensInTheSameView(DocumentViewMode mode)
        {
            var saved = new Dictionary<string, string>();
            var first = LoadLayout();
            first.ViewMode = mode;
            ViewModeMemory.Remember(first, saved);

            var next = LoadLayout();
            ViewModeMemory.Restore(next, saved);

            Assert.Equal(mode, next.ViewMode);
        }

        [Fact]
        [DisplayName("Each kind of document keeps its own view")]
        public void Restore_OtherKind_KeepsItsOwnView()
        {
            var saved = new Dictionary<string, string>();
            var layout = LoadLayout();
            layout.ViewMode = DocumentViewMode.Preview;
            ViewModeMemory.Remember(layout, saved);

            var schema = LoadSchema();
            ViewModeMemory.Restore(schema, saved);

            Assert.Equal(DocumentViewMode.Design, schema.ViewMode);
        }

        [Theory]
        [InlineData("Preview")]
        [InlineData("Unknown")]
        [InlineData("42")]
        [DisplayName("A view the document cannot show, or one that is not a view, leaves it in Design")]
        public void Restore_ViewTheDocumentCannotShow_StaysInDesign(string mode)
        {
            var schema = LoadSchema();
            var saved = new Dictionary<string, string> { [schema.GetType().Name] = mode };

            ViewModeMemory.Restore(schema, saved);

            Assert.Equal(DocumentViewMode.Design, schema.ViewMode);
        }
    }
}
