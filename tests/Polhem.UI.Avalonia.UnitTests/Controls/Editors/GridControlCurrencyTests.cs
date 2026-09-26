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
    /// GridControl per-cell currency awareness: an amount column resolves its decimals from the current value of the row's currency column (CUKY).
    /// Different currencies in the same column give different decimals; an empty row currency falls back to the grid's default currency; without CurrencySettings the column's baked format applies.
    /// </summary>
    public class GridControlCurrencyTests
    {
        private static CurrencySettings Currencies() =>
        [
            new CurrencyItem("USD", 0.01m, "$", "US Dollar"),
            new CurrencyItem("JPY", 1m, "¥", "Japanese Yen"),
            new CurrencyItem("BHD", 0.001m, "BD", "Bahraini Dinar"),
        ];

        // Renders the amount cell for a row via the read-only interactive cell (a TextBlock whose
        // Text is the currency-aware FormatCellForColumn result).
        private static string AmountCellText(GridControl grid, DataTable table, int rowIndex, string bakedFormat = "")
        {
            var column = new LayoutColumn("amount", "金額", ControlType.NumericEdit)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "sys_currency",
                NumberFormat = bakedFormat,
            };
            var method = typeof(GridControl).GetMethod(
                "BuildInteractiveCell", BindingFlags.NonPublic | BindingFlags.Instance);
            var cell = (Control)method!.Invoke(grid, new object?[] { table.DefaultView[rowIndex], column })!;
            return Assert.IsType<TextBlock>(cell).Text ?? string.Empty;
        }

        private static DataTable AmountTable(params (decimal Amount, string Currency)[] rows)
        {
            var table = new DataTable("OrderLine");
            table.Columns.Add("amount", typeof(decimal));
            table.Columns.Add("sys_currency", typeof(string));
            foreach (var (amount, currency) in rows)
                table.Rows.Add(amount, currency);
            return table;
        }

        private static GridControl BindGrid(DataTable table, CurrencySettings? currencies, string defaultCode = "")
        {
            var layout = new LayoutGrid("OrderLine", "明細");
            layout.Columns!.Add(new LayoutColumn("amount", "金額", ControlType.NumericEdit)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "sys_currency",
            });
            layout.Columns.Add(new LayoutColumn("sys_currency", "幣別", ControlType.TextEdit));

            var grid = new GridControl { CurrencySettings = currencies, DefaultCurrencyCode = defaultCode };
            grid.Bind(layout, table);
            return grid;
        }

        [Fact]
        [DisplayName("USD/JPY/BHD rows in the same column each get the decimals of their currency (2/0/3)")]
        public void AmountColumn_PerRowCurrency_DifferentDecimals()
        {
            var table = AmountTable((1234.567m, "USD"), (1234.567m, "JPY"), (1234.567m, "BHD"));
            var grid = BindGrid(table, Currencies());

            Assert.Equal("1,234.57", AmountCellText(grid, table, 0));   // USD: 2 decimals.
            Assert.Equal("1,235", AmountCellText(grid, table, 1));      // JPY: 0 decimals.
            Assert.Equal("1,234.567", AmountCellText(grid, table, 2));  // BHD: 3 decimals.
        }

        [Fact]
        [DisplayName("Currency resolution overrides the column's baked format (a JPY row ignores N2 and shows 0 decimals)")]
        public void AmountColumn_CurrencyResolution_OverridesBakedFormat()
        {
            var table = AmountTable((1234.567m, "JPY"));
            var grid = BindGrid(table, Currencies());

            // The currency resolution (JPY, 0 decimals) must override the baked "N2".
            Assert.Equal("1,235", AmountCellText(grid, table, 0, bakedFormat: "N2"));
        }

        [Fact]
        [DisplayName("An empty row currency falls back to the grid's default currency (the master document currency or the company home currency)")]
        public void AmountColumn_EmptyRowCurrency_FallsBackToDefaultCurrencyCode()
        {
            var table = AmountTable((1234.567m, string.Empty));
            var grid = BindGrid(table, Currencies(), defaultCode: "JPY");

            Assert.Equal("1,235", AmountCellText(grid, table, 0)); // Falls back to JPY: 0 decimals.
        }

        [Fact]
        [DisplayName("Without CurrencySettings the amount column uses the column's baked format (currency awareness off)")]
        public void AmountColumn_NoCurrencySettings_UsesBakedFormat()
        {
            var table = AmountTable((1234.567m, "JPY"));
            var grid = BindGrid(table, currencies: null);

            // The column's baked format (N2 here) is kept because currency awareness is off.
            Assert.Equal("1,234.57", AmountCellText(grid, table, 0, bakedFormat: "N2"));
        }
    }
}
