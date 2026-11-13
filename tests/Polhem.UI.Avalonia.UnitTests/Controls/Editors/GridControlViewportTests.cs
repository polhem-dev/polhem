using System.ComponentModel;
using System.Data;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// How a <see cref="GridControl"/> returns to a place it was handed: rows are found again by reference or by their
    /// <see cref="SysFields.RowId"/>, a place handed to a grid not yet shown is kept until it is, and a row that is gone
    /// is passed over.
    /// </summary>
    /// <remarks>
    /// No Avalonia platform runs, so the grid realizes no rows and captures no visible ones itself; the places are built
    /// by hand.
    /// </remarks>
    public class GridControlViewportTests
    {
        private static DataTable BuildRows(params Guid[] rowIds)
        {
            var table = new DataTable("Items");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add("name", typeof(string));
            foreach (var rowId in rowIds)
                table.Rows.Add(rowId, "item");
            return table;
        }

        private static GridControl BindGrid(DataTable rows)
        {
            var layout = new LayoutGrid("Items", "Items");
            layout.Columns!.Add(new LayoutColumn("name", "Name", ControlType.TextEdit));
            var grid = new GridControl();
            grid.Bind(layout, rows);
            return grid;
        }

        [Fact]
        [DisplayName("A place taken from rows that were since replaced selects the row with the same sys_rowid")]
        public void RestoreViewport_ReplacedRows_SelectsBySysRowId()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var oldRows = BuildRows(first, second);
            var grid = BindGrid(BuildRows(first, second));
            var visible = new List<(GridControl.RowRef, bool)>
            {
                (new GridControl.RowRef(oldRows.Rows[0], first), true),
                (new GridControl.RowRef(oldRows.Rows[1], second), false),
            };

            grid.RestoreViewport(new GridControl.RowViewport(visible, new GridControl.RowRef(oldRows.Rows[1], second)));

            var selected = Assert.IsType<DataRowView>(grid.SelectedItem);
            Assert.Equal(second, selected.Row[SysFields.RowId]);
        }

        [Fact]
        [DisplayName("Rows that are no longer in the table are passed over and nothing is selected")]
        public void RestoreViewport_RowsGone_SelectsNothing()
        {
            var gone = BuildRows(Guid.NewGuid());
            var grid = BindGrid(BuildRows(Guid.NewGuid()));
            var visible = new List<(GridControl.RowRef, bool)> { (new GridControl.RowRef(gone.Rows[0], null), true) };

            grid.RestoreViewport(new GridControl.RowViewport(visible, new GridControl.RowRef(gone.Rows[0], null)));

            Assert.Null(grid.SelectedItem);
        }

        [Fact]
        [DisplayName("A row still in the table is found again by reference")]
        public void RestoreViewport_SameRow_SelectsByReference()
        {
            var rows = BuildRows(Guid.NewGuid(), Guid.NewGuid());
            var grid = BindGrid(rows);

            grid.RestoreViewport(new GridControl.RowViewport([], new GridControl.RowRef(rows.Rows[1], null)));

            Assert.Same(rows.Rows[1], Assert.IsType<DataRowView>(grid.SelectedItem).Row);
        }

        [Fact]
        [DisplayName("A grid without a table finds no row to return to")]
        public void RestoreViewport_NoTable_SelectsNothing()
        {
            var rows = BuildRows(Guid.NewGuid());
            var grid = new GridControl();

            grid.RestoreViewport(new GridControl.RowViewport([], new GridControl.RowRef(rows.Rows[0], null)));

            Assert.Null(grid.SelectedItem);
        }

        [Fact]
        [DisplayName("A place handed to a grid not yet shown is what the grid reports until it is shown")]
        public void RestoreViewportWhenShown_NotShown_KeepsPendingPlace()
        {
            var rows = BuildRows(Guid.NewGuid());
            var grid = BindGrid(rows);
            var place = new GridControl.RowViewport([], new GridControl.RowRef(rows.Rows[0], null));

            grid.RestoreViewportWhenShown(place);

            Assert.Equal(place, grid.CaptureViewport());
        }

        [Fact]
        [DisplayName("A grid that was never shown captures no visible rows, and its selected row by sys_rowid")]
        public void CaptureViewport_NotShown_CapturesSelectionOnly()
        {
            var rowId = Guid.NewGuid();
            var rows = BuildRows(rowId);
            var grid = BindGrid(rows);
            grid.SelectedItem = rows.DefaultView[0];

            var place = grid.CaptureViewport();

            Assert.Empty(place.Visible);
            Assert.Equal(new GridControl.RowRef(rows.Rows[0], rowId), place.Selected);
        }

        [Fact]
        [DisplayName("A selected row without a sys_rowid column is captured by reference only")]
        public void CaptureViewport_RowWithoutRowId_CapturesReferenceOnly()
        {
            var rows = new DataTable("Items");
            rows.Columns.Add("name", typeof(string));
            rows.Rows.Add("item");
            var grid = BindGrid(rows);
            grid.SelectedItem = rows.DefaultView[0];

            var place = grid.CaptureViewport();

            Assert.Equal(new GridControl.RowRef(rows.Rows[0], null), place.Selected);
        }
    }
}
