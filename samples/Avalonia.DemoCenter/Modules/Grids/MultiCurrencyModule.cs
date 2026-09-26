using System.Globalization;
using Avalonia.Controls;
using Polhem.Base;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.Grids
{
    /// <summary>
    /// Multi-currency amounts: an order-line grid where the <c>amount</c> column (NumberKind Amount)
    /// resolves its decimals per row from that row's <c>sys_currency</c> field via
    /// <c>GridControl.CurrencySettings</c> — USD shows 2 places, JPY 0, BHD 3. The document-currency
    /// toggle rewrites every row's currency and re-renders (same stored values, different decimals).
    /// A manual footer uses <c>AmountColumnSummary</c>: the original-currency total shows only when all
    /// rows share one currency (mixed → no total), while the home-currency total always shows.
    /// </summary>
    public sealed class MultiCurrencyModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Grid";

        /// <inheritdoc/>
        public override string Title => "Multi-currency amounts";

        /// <inheritdoc/>
        public override string Description =>
            "Amount columns resolve their decimals from the row's currency column, sys_currency (USD 2 / JPY 0 / BHD 3); switching the document currency changes the decimals of the same amounts live. The original-currency total is shown only when every row has the same currency, not for mixed currencies; the home-currency (TWD) total is always shown.";

        // Curated client currency master (the subset this demo uses).
        private static CurrencySettings BuildCurrencies() =>
        [
            new CurrencyItem("USD", 0.01m, "$", "US Dollar"),
            new CurrencyItem("JPY", 1m, "¥", "Japanese Yen"),
            new CurrencyItem("BHD", 0.001m, "BD", "Bahraini Dinar"),
            new CurrencyItem("TWD", 0.01m, "NT$", "New Taiwan Dollar"),
        ];

        // (currency, home_amount in TWD) seeded per line; amount is the original-currency value.
        private static readonly (string Product, decimal Amount, string Currency, decimal HomeAmount)[] Lines =
        {
            ("Widget", 1234.567m, "USD", 39505m),
            ("Gadget", 5000.4m, "JPY", 1050m),
            ("Gizmo", 250.125m, "BHD", 21260m),
        };

        private static readonly string[] s_documentModes = ["Mixed (as seeded)", "All USD", "All JPY"];

        private readonly CurrencySettings _currencies = BuildCurrencies();

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = BuildData();
            var table = data.DataSet.Tables["OrderLine"]!;

            var grid = new GridControl
            {
                MinHeight = 200,
                CurrencySettings = _currencies,
            };
            grid.Bind(BuildLayout(), table);

            var originalTotal = new TextBlock();
            var homeTotal = new TextBlock();
            void RefreshTotals()
            {
                originalTotal.Text = "Original-currency total: " + FormatOriginalTotal(table);
                homeTotal.Text = "Home-currency total (TWD): " + FormatHomeTotal(table);
            }
            RefreshTotals();

            var modeIndex = 0;
            var toggle = new Button { Content = "Document currency: " + s_documentModes[modeIndex] };
            toggle.Click += (_, _) =>
            {
                modeIndex = (modeIndex + 1) % s_documentModes.Length;
                toggle.Content = "Document currency: " + s_documentModes[modeIndex];
                ApplyDocumentCurrency(table, modeIndex);
                grid.RefreshRows();
                RefreshTotals();
            };

            return new ScrollViewer
            {
                Content = DataEditorParts.Section(
                    "Multi-currency amounts (amount decimals follow the row currency)",
                    "The amount column is bound to sys_currency (CUKY); switching the document currency changes the decimals of the same amounts. The original-currency total is shown only for a single currency, not for mixed ones; the home-currency total is always shown.",
                    toggle, grid, originalTotal, homeTotal),
            };
        }

        private static void ApplyDocumentCurrency(System.Data.DataTable table, int modeIndex)
        {
            // Mode 0 = original per-row currencies; 1 = all USD; 2 = all JPY.
            for (int i = 0; i < table.Rows.Count; i++)
            {
                table.Rows[i]["sys_currency"] = modeIndex switch
                {
                    1 => "USD",
                    2 => "JPY",
                    _ => Lines[i].Currency,
                };
            }
        }

        private string FormatOriginalTotal(System.Data.DataTable table)
        {
            var cells = table.Rows.Cast<System.Data.DataRow>()
                .Select(r => (ValueUtilities.CDecimal(r["amount"]), ValueUtilities.CStr(r["sys_currency"])));
            var total = AmountColumnSummary.TryComputeTotal(cells);
            if (total is null) { return "— mixed currencies, no total"; }

            // All rows share one currency here → format by it.
            string code = ValueUtilities.CStr(table.Rows[0]["sys_currency"]);
            string format = NumberFormatResolver.ResolveFormat(
                NumberKind.Amount, new RoundingContext { CurrencySettings = _currencies }, code);
            return total.Value.ToString(format, CultureInfo.InvariantCulture) + " " + code;
        }

        private string FormatHomeTotal(System.Data.DataTable table)
        {
            var cells = table.Rows.Cast<System.Data.DataRow>()
                .Select(r => (ValueUtilities.CDecimal(r["home_amount"]), "TWD"));
            var total = AmountColumnSummary.TryComputeTotal(cells) ?? 0m;
            string format = NumberFormatResolver.ResolveFormat(
                NumberKind.Amount, new RoundingContext { CurrencySettings = _currencies }, "TWD");
            return total.ToString(format, CultureInfo.InvariantCulture) + " TWD";
        }

        private static LayoutGrid BuildLayout()
        {
            var layout = new LayoutGrid("OrderLine", "Order lines");
            layout.Columns!.Add(new LayoutColumn("product", "Product", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("amount", "Amount (original)", ControlType.NumericEdit)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "sys_currency",
            });
            layout.Columns.Add(new LayoutColumn("sys_currency", "Currency", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("home_amount", "Amount (home)", ControlType.NumericEdit)
            {
                NumberKind = NumberKind.Amount,
                CurrencyField = "local_currency",
            });
            layout.Columns.Add(new LayoutColumn("local_currency", "Home currency", ControlType.TextEdit));
            return layout;
        }

        private static FormDataObject BuildData()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add("order_no", "Order No.", FieldDbType.String);

            var line = schema.Tables.Add("OrderLine", "Lines");
            line.Fields!.Add("product", "Product", FieldDbType.String);
            line.Fields!.Add(new FormField("amount", "Amount (original)", FieldDbType.Decimal) { NumberKind = NumberKind.Amount, CurrencyField = "sys_currency" });
            line.Fields!.Add("sys_currency", "Currency", FieldDbType.String);
            line.Fields!.Add(new FormField("home_amount", "Amount (home)", FieldDbType.Decimal) { NumberKind = NumberKind.Amount, CurrencyField = "local_currency" });
            line.Fields!.Add("local_currency", "Home currency", FieldDbType.String);

            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("order_no", "SO-100");

            var lines = data.DataSet.Tables["OrderLine"]!;
            foreach (var (product, amount, currency, homeAmount) in Lines)
                lines.Rows.Add(product, amount, currency, homeAmount, "TWD");
            return data;
        }
    }
}
