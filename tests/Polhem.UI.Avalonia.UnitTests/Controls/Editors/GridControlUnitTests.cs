using System.ComponentModel;
using System.Data;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Definition;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// GridControl per-cell unit awareness: a quantity column resolves its decimals from the current value of the row's unit column (UNIT).
    /// Different units in the same column give different decimals; an empty row unit falls back to the column's baked format, as does a missing UnitSettings.
    /// </summary>
    public class GridControlUnitTests
    {
        private static UnitSettings Units() =>
        [
            new UnitItem("PCS", 0, "count", "Pieces"),
            new UnitItem("KG", 3, "weight", "Kilogram"),
            new UnitItem("M", 2, "length", "Metre"),
        ];

        private static string QtyCellText(GridControl grid, DataTable table, int rowIndex, string bakedFormat = "")
        {
            var column = new LayoutColumn("qty", "數量", ControlType.NumericEdit)
            {
                NumberKind = NumberKind.Quantity,
                UnitField = "qty_uom",
                NumberFormat = bakedFormat,
            };
            var method = typeof(GridControl).GetMethod(
                "BuildInteractiveCell", BindingFlags.NonPublic | BindingFlags.Instance);
            var cell = (Control)method!.Invoke(grid, new object?[] { table.DefaultView[rowIndex], column })!;
            return Assert.IsType<TextBlock>(cell).Text ?? string.Empty;
        }

        private static DataTable QtyTable(params (decimal Qty, string Unit)[] rows)
        {
            var table = new DataTable("OrderLine");
            table.Columns.Add("qty", typeof(decimal));
            table.Columns.Add("qty_uom", typeof(string));
            foreach (var (qty, unit) in rows)
                table.Rows.Add(qty, unit);
            return table;
        }

        private static GridControl BindGrid(DataTable table, UnitSettings? units)
        {
            var layout = new LayoutGrid("OrderLine", "明細");
            layout.Columns!.Add(new LayoutColumn("qty", "數量", ControlType.NumericEdit)
            {
                NumberKind = NumberKind.Quantity,
                UnitField = "qty_uom",
            });
            layout.Columns.Add(new LayoutColumn("qty_uom", "單位", ControlType.TextEdit));

            var grid = new GridControl { UnitSettings = units };
            grid.Bind(layout, table);
            return grid;
        }

        [Fact]
        [DisplayName("PCS/KG/M rows in the same column each get the decimals of their unit (0/3/2)")]
        public void QtyColumn_PerRowUnit_DifferentDecimals()
        {
            var table = QtyTable((12.345m, "PCS"), (12.345m, "KG"), (12.345m, "M"));
            var grid = BindGrid(table, Units());

            Assert.Equal("12", QtyCellText(grid, table, 0));      // PCS: 0 decimals.
            Assert.Equal("12.345", QtyCellText(grid, table, 1));  // KG: 3 decimals.
            Assert.Equal("12.35", QtyCellText(grid, table, 2));   // M: 2 decimals (12.345 becomes 12.35).
        }

        [Fact]
        [DisplayName("An empty row unit falls back to the column's baked format (no unit resolution)")]
        public void QtyColumn_EmptyRowUnit_UsesBakedFormat()
        {
            var table = QtyTable((12.345m, string.Empty));
            var grid = BindGrid(table, Units());

            // An empty row unit is not resolved, so the column's baked format (N0 here) applies.
            Assert.Equal("12", QtyCellText(grid, table, 0, bakedFormat: "N0"));
        }

        [Fact]
        [DisplayName("Without UnitSettings the quantity column uses the column's baked format (unit awareness off)")]
        public void QtyColumn_NoUnitSettings_UsesBakedFormat()
        {
            var table = QtyTable((12.345m, "KG"));
            var grid = BindGrid(table, units: null);

            Assert.Equal("12.35", QtyCellText(grid, table, 0, bakedFormat: "N2"));
        }
    }
}
