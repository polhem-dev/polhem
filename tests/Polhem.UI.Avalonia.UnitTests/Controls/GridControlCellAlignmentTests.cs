using System.ComponentModel;
using System.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// How <see cref="GridControl"/> lays out the text of a display cell. The drop-down, date and check box cells were
    /// vertically centered while the text cells sat at the top, and numeric columns were left-aligned while the Blazor
    /// grids right-align them.
    /// </summary>
    public class GridControlCellAlignmentTests
    {
        private static (GridControl Grid, DataRowView Row) Bind(LayoutColumn column)
        {
            var rows = new DataTable("Line");
            rows.Columns.Add("item", typeof(string));
            rows.Columns.Add("qty", typeof(decimal));
            rows.Rows.Add("Bolt", 3m);
            var layout = new LayoutGrid("Line", "Line");
            layout.Columns!.Add(column);
            var grid = new GridControl();
            grid.Bind(layout, rows);
            return (grid, rows.DefaultView[0]);
        }

        [Fact]
        [DisplayName("A text cell is vertically centered and left-aligned")]
        public void BuildTextCell_TextColumn_CenteredAndLeftAligned()
        {
            var column = new LayoutColumn("item", "Item", ControlType.TextEdit);
            var (grid, row) = Bind(column);

            var cell = grid.BuildTextCell(row, column);

            Assert.Equal(VerticalAlignment.Center, cell.VerticalAlignment);
            Assert.Equal(TextAlignment.Left, cell.TextAlignment);
        }

        [Fact]
        [DisplayName("A numeric cell is vertically centered and right-aligned")]
        public void BuildTextCell_NumericColumn_CenteredAndRightAligned()
        {
            var column = new LayoutColumn("qty", "Qty", ControlType.NumericEdit);
            var (grid, row) = Bind(column);

            var cell = grid.BuildTextCell(row, column);

            Assert.Equal(VerticalAlignment.Center, cell.VerticalAlignment);
            Assert.Equal(TextAlignment.Right, cell.TextAlignment);
        }
    }
}
