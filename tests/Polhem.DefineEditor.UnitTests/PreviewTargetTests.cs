using System.ComponentModel;
using Polhem.Definition.Layouts;
using Polhem.DefineEditor.BrowserPreview;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// <see cref="PreviewTarget"/>: the part of a previewed form a browser page names by its position in the layout.
    /// </summary>
    public sealed class PreviewTargetTests
    {
        private readonly FormLayout _layout;
        private readonly LayoutSection _main;
        private readonly LayoutSection _other;
        private readonly LayoutGrid _grid;

        public PreviewTargetTests()
        {
            _layout = new FormLayout { LayoutId = "Order", ProgId = "Order" };
            _main = new LayoutSection { Name = "Main" };
            _main.Fields!.Add(new LayoutField { FieldName = "sys_id" });
            _main.Fields.Add(new LayoutField { FieldName = "hidden", Visible = false });
            _main.Fields.Add(new LayoutField { FieldName = "sys_name" });
            _other = new LayoutSection { Name = "Other" };
            _other.Fields!.Add(new LayoutField { FieldName = "remark" });
            _layout.Sections!.Add(_main);
            _layout.Sections.Add(_other);
            _grid = new LayoutGrid { TableName = "OrderDetail" };
            _grid.Columns!.Add(new LayoutColumn { FieldName = "sys_rowid", Visible = false });
            _grid.Columns.Add(new LayoutColumn { FieldName = "quantity" });
            _layout.Details!.Add(_grid);
        }

        [Fact]
        [DisplayName("Each part locates to a target that resolves back to the same object")]
        public void Locate_ThenResolve_ReturnsTheSameObject()
        {
            object[] parts = [_main, _other, _main.Fields![2], _other.Fields![0], _grid, _grid.Columns![1]];

            foreach (var part in parts)
            {
                var target = PreviewTarget.Locate(_layout, part);
                Assert.NotNull(target);
                Assert.Same(part, target.Value.Resolve(_layout));
            }
        }

        [Fact]
        [DisplayName("A field and a column count only the visible ones before them, as the pages draw them")]
        public void Locate_AfterHiddenItems_CountsOnlyVisibleOnes()
        {
            Assert.Equal(new PreviewTarget(PreviewTargetKind.Field, 0, 1), PreviewTarget.Locate(_layout, _main.Fields![2]));
            Assert.Equal(new PreviewTarget(PreviewTargetKind.Column, 0, 0), PreviewTarget.Locate(_layout, _grid.Columns![1]));
        }

        [Fact]
        [DisplayName("A hidden field, the layout itself and an object of another layout have no target")]
        public void Locate_PartNotDrawn_ReturnsNull()
        {
            Assert.Null(PreviewTarget.Locate(_layout, _main.Fields![1]));
            Assert.Null(PreviewTarget.Locate(_layout, _layout));
            Assert.Null(PreviewTarget.Locate(_layout, new LayoutSection { Name = "Main" }));
            Assert.Null(PreviewTarget.Locate(_layout, null));
        }

        [Theory]
        [InlineData(PreviewTargetKind.Section, 5, -1)]
        [InlineData(PreviewTargetKind.Field, 0, 9)]
        [InlineData(PreviewTargetKind.Column, 0, -1)]
        [InlineData(PreviewTargetKind.Grid, -1, -1)]
        [DisplayName("A target outside the layout resolves to nothing")]
        public void Resolve_OutOfRange_ReturnsNull(PreviewTargetKind kind, int index, int item)
        {
            Assert.Null(new PreviewTarget(kind, index, item).Resolve(_layout));
        }
    }
}
