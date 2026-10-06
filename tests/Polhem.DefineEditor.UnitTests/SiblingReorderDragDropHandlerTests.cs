using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// Reordering by drag in the FormLayout tree: sections under the Sections folder, and fields directly
    /// under a section.
    /// </summary>
    public sealed class SiblingReorderDragDropHandlerTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-reorder-" + Guid.NewGuid().ToString("N"));
        private readonly FormLayoutDocumentViewModel _document;

        public SiblingReorderDragDropHandlerTests()
        {
            Directory.CreateDirectory(_directory);
            var layout = new FormLayout { LayoutId = "Employee", ProgId = "Employee", Caption = "Employee" };
            foreach (var name in new[] { "Main", "Address" })
            {
                var section = new LayoutSection { Name = name, Caption = name };
                foreach (var field in new[] { "a", "b", "c" })
                    section.Fields!.Add(new LayoutField { FieldName = $"{name}_{field}", Caption = field });
                layout.Sections!.Add(section);
            }
            var path = Path.Combine(_directory, "Employee.FormLayout.xml");
            XmlCodec.SerializeToFile(layout, path);
            _document = FormLayoutDocumentViewModel.Load(path);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private ITreeNodeDragDropHandler Handler => _document.DragDropHandler;

        private ObjectTreeNode Sections => _document.RootNode.Children[0];

        [Fact]
        [DisplayName("Dropping a field after a sibling reorders the section's fields and marks the document dirty")]
        public void Drop_FieldAfterSibling_ReordersFields()
        {
            var main = Sections.Children[0];
            var first = main.Children[0];
            var last = main.Children[2];

            Assert.True(Handler.CanDrag(first));
            Assert.True(Handler.CanDrop(first, last, TreeNodeDropPosition.After));
            Handler.Drop(first, last, TreeNodeDropPosition.After);

            Assert.Equal(["Main_b", "Main_c", "Main_a"], ((LayoutSection)main.Value).Fields!.Select(f => f.FieldName));
            Assert.True(_document.IsDirty);
        }

        [Fact]
        [DisplayName("Dropping a section before another reorders the layout's sections")]
        public void Drop_SectionBeforeSibling_ReordersSections()
        {
            var address = Sections.Children[1];

            Handler.Drop(address, Sections.Children[0], TreeNodeDropPosition.Before);

            Assert.Equal(["Address", "Main"], _document.Root.Sections!.Select(s => s.Name));
        }

        [Fact]
        [DisplayName("A field cannot be dropped into another section, and folders and the root cannot be dragged")]
        public void CanDragAndDrop_OutsideOwnCollection_AreRefused()
        {
            var mainField = Sections.Children[0].Children[0];
            var addressField = Sections.Children[1].Children[0];

            Assert.False(Handler.CanDrop(mainField, addressField, TreeNodeDropPosition.Before));
            Assert.False(Handler.CanDrag(Sections));
            Assert.False(Handler.CanDrag(_document.RootNode));
        }
    }
}
