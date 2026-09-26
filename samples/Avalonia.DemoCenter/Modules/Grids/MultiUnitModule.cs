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
    /// Multi-unit quantities: an order-line grid where the <c>qty</c> column (NumberKind Quantity)
    /// resolves its decimals per row from that row's <c>qty_uom</c> unit field via
    /// <c>GridControl.UnitSettings</c> — PCS shows 0 places, KG 3, M 2. Same stored values, different
    /// decimals per row. A manual footer uses <c>AmountColumnSummary</c>: the quantity total shows only
    /// when all rows share one unit (mixed → no total), mirroring the mixed-currency rule.
    /// </summary>
    public sealed class MultiUnitModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Grid";

        /// <inheritdoc/>
        public override string Title => "Multi-unit quantities";

        /// <inheritdoc/>
        public override string Description =>
            "The quantity column resolves its decimals from the row's unit column, qty_uom (PCS 0 / KG 3 / M 2); switching the unit changes the decimals of the same quantities live. The quantity total is shown only when every row has the same unit, not for mixed units.";

        // Curated client unit master (the subset this demo uses).
        private static UnitSettings BuildUnits() =>
        [
            new UnitItem("PCS", 0, "count", "Pieces"),
            new UnitItem("KG", 3, "weight", "Kilogram"),
            new UnitItem("M", 2, "length", "Metre"),
        ];

        private static readonly (string Product, decimal Qty, string Unit)[] Lines =
        {
            ("Bolt", 12.345m, "PCS"),
            ("Steel", 12.345m, "KG"),
            ("Cable", 12.345m, "M"),
        };

        private static readonly string[] s_unitModes = ["Mixed (as seeded)", "All KG", "All PCS"];

        private readonly UnitSettings _units = BuildUnits();

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = BuildData();
            var table = data.DataSet.Tables["OrderLine"]!;

            var grid = new GridControl
            {
                MinHeight = 200,
                UnitSettings = _units,
            };
            grid.Bind(BuildLayout(), table);

            var qtyTotal = new TextBlock();
            void RefreshTotal() => qtyTotal.Text = "Quantity total: " + FormatQtyTotal(table);
            RefreshTotal();

            var modeIndex = 0;
            var toggle = new Button { Content = "Unit of measure: " + s_unitModes[modeIndex] };
            toggle.Click += (_, _) =>
            {
                modeIndex = (modeIndex + 1) % s_unitModes.Length;
                toggle.Content = "Unit of measure: " + s_unitModes[modeIndex];
                ApplyUnit(table, modeIndex);
                grid.RefreshRows();
                RefreshTotal();
            };

            return new ScrollViewer
            {
                Content = DataEditorParts.Section(
                    "Multi-unit quantities (quantity decimals follow the row unit)",
                    "The qty column is bound to qty_uom (UNIT); switching the unit changes the decimals of the same quantities. The total is shown only for a single unit, not for mixed ones.",
                    toggle, grid, qtyTotal),
            };
        }

        private static void ApplyUnit(System.Data.DataTable table, int modeIndex)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                table.Rows[i]["qty_uom"] = modeIndex switch
                {
                    1 => "KG",
                    2 => "PCS",
                    _ => Lines[i].Unit,
                };
            }
        }

        private string FormatQtyTotal(System.Data.DataTable table)
        {
            var cells = table.Rows.Cast<System.Data.DataRow>()
                .Select(r => (ValueUtilities.CDecimal(r["qty"]), ValueUtilities.CStr(r["qty_uom"])));
            var total = AmountColumnSummary.TryComputeTotal(cells);
            if (total is null) { return "— mixed units, no total"; }

            string code = ValueUtilities.CStr(table.Rows[0]["qty_uom"]);
            string format = NumberFormatResolver.ResolveFormat(
                NumberKind.Quantity, new RoundingContext { UnitSettings = _units }, code);
            return total.Value.ToString(format, CultureInfo.InvariantCulture) + " " + code;
        }

        private static LayoutGrid BuildLayout()
        {
            var layout = new LayoutGrid("OrderLine", "Order lines");
            layout.Columns!.Add(new LayoutColumn("product", "Product", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("qty", "Quantity", ControlType.NumericEdit)
            {
                NumberKind = NumberKind.Quantity,
                UnitField = "qty_uom",
            });
            layout.Columns.Add(new LayoutColumn("qty_uom", "Unit", ControlType.TextEdit));
            return layout;
        }

        private static FormDataObject BuildData()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add("order_no", "Order No.", FieldDbType.String);

            var line = schema.Tables.Add("OrderLine", "Lines");
            line.Fields!.Add("product", "Product", FieldDbType.String);
            line.Fields!.Add(new FormField("qty", "Quantity", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity, UnitField = "qty_uom" });
            line.Fields!.Add("qty_uom", "Unit", FieldDbType.String);

            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("order_no", "SO-200");

            var lines = data.DataSet.Tables["OrderLine"]!;
            foreach (var (product, qty, unit) in Lines)
                lines.Rows.Add(product, qty, unit);
            return data;
        }
    }
}
