using Avalonia.Controls;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.Grids
{
    /// <summary>
    /// Numeric formatting: an order-line grid whose numeric columns each carry a
    /// <see cref="NumberKind"/>, so each resolves its own decimal places (unit price N4, amount N2,
    /// discount P2). Quantity and weight bind a unit field and resolve per row from that unit through
    /// <c>GridControl.UnitSettings</c> (PCS 0, KG 3), never from the company. The company toggle
    /// switches between framework defaults and company overrides, re-resolving the company-sourced
    /// formats; double-clicking a numeric cell edits it in place with <c>NumericEdit</c> (full precision
    /// on focus, formatted on blur).
    /// </summary>
    public sealed class NumberFormatModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Grid";

        /// <inheritdoc/>
        public override string Title => "Number formatting";

        /// <inheritdoc/>
        public override string Description =>
            "Each column resolves its display decimals from its NumberKind (unit price N4 / amount N2 / discount P2). Quantity and weight are bound to unit columns and follow the row's unit (PCS 0 / KG 3), independent of the company. Switching the company applies its overridden decimals live. Double-click a numeric cell to edit it in place with NumericEdit (full precision on focus, formatted on blur; display rounding is not written back).";

        // Numeric order-line columns: (data field, caption, semantic kind, unit field). Quantity and
        // weight must bind a unit field; the other kinds take none.
        private static readonly (string Field, string Caption, NumberKind Kind, string UnitField)[] NumericColumns =
        {
            ("quantity", "Quantity", NumberKind.Quantity, "quantity_uom"),
            ("unit_price", "Unit price", NumberKind.UnitPrice, ""),
            ("amount", "Amount", NumberKind.Amount, ""),
            ("gross_weight", "Weight", NumberKind.Weight, "weight_uom"),
            ("discount_pct", "Discount", NumberKind.Percent, ""),
        };

        // Curated client unit master (the subset this demo uses).
        private static UnitSettings BuildUnits() =>
        [
            new UnitItem("PCS", 0, "count", "Pieces"),
            new UnitItem("KG", 3, "weight", "Kilogram"),
        ];

        private readonly UnitSettings _units = BuildUnits();

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = BuildData();
            var grid = new GridControl { MinHeight = 240, EditMode = GridEditMode.InCell, UnitSettings = _units };
            grid.Bind(data, BuildLayout(company: null));

            var useOverrides = false;
            var toggle = new Button { Content = CompanyLabel(useOverrides) };
            toggle.Click += (_, _) =>
            {
                useOverrides = !useOverrides;
                toggle.Content = CompanyLabel(useOverrides);
                grid.Bind(data, BuildLayout(useOverrides ? CompanyWithOverrides() : null));
            };

            return new ScrollViewer
            {
                Content = DataEditorParts.Section(
                    "Number formatting (NumberKind → decimals)",
                    "The same data, with each column resolving different decimals from its NumberKind. Switching the company applies its overridden decimals (unit price 4→2, discount P2→P4) live; quantity and weight follow the row's unit and are not affected by the company. Unit prices are stored at full precision; display rounding is not written back.",
                    toggle, grid),
            };
        }

        private static string CompanyLabel(bool useOverrides)
            => useOverrides ? "Company B (overridden decimals) — click to switch back to A" : "Company A (framework defaults) — click to switch to B";

        private static LayoutGrid BuildLayout(CompanyInfo? company)
        {
            var layout = new LayoutGrid("OrderLine", "Order lines");
            layout.Columns!.Add(new LayoutColumn("product", "Product", ControlType.TextEdit));
            foreach (var (field, caption, kind, unitField) in NumericColumns)
            {
                layout.Columns.Add(new LayoutColumn(field, caption, ControlType.NumericEdit)
                {
                    NumberKind = kind,
                    UnitField = unitField,
                    NumberFormat = NumberFormatResolver.ResolveFormat(kind, company),
                });
                if (!string.IsNullOrEmpty(unitField))
                    layout.Columns.Add(new LayoutColumn(unitField, "Unit", ControlType.TextEdit));
            }
            return layout;
        }

        private static CompanyInfo CompanyWithOverrides()
        {
            var company = new CompanyInfo { CompanyId = "B", CompanyName = "Company B", DefaultCurrency = "USD" };
            company.NumberFormats.Add(new NumberFormatItem(NumberKind.UnitPrice, 2));
            company.NumberFormats.Add(new NumberFormatItem(NumberKind.Percent, 4));
            return company;
        }

        private static FormDataObject BuildData()
        {
            var schema = new FormSchema("Order", "Order");
            schema.Tables!.Add("Order", "Order").Fields!.Add("order_no", "Order No.", FieldDbType.String);

            var line = schema.Tables.Add("OrderLine", "Lines");
            line.Fields!.Add("product", "Product", FieldDbType.String);
            foreach (var (field, caption, kind, unitField) in NumericColumns)
                line.Fields!.Add(new FormField(field, caption, FieldDbType.Decimal) { NumberKind = kind, UnitField = unitField });
            line.Fields!.Add("quantity_uom", "Quantity unit", FieldDbType.String);
            line.Fields!.Add("weight_uom", "Weight unit", FieldDbType.String);

            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("order_no", "SO-001");

            var lines = data.DataSet.Tables["OrderLine"]!;
            // Columns are appended in schema order: product, quantity, unit_price, amount,
            // gross_weight, discount_pct, quantity_uom, weight_uom. Percent is stored as a fraction
            // (0.055 renders "5.50%"). unit_price carries more precision than N4 shows, to demonstrate
            // preserve-on-display.
            lines.Rows.Add("Widget", 3m, 12.3456789m, 37.04m, 1.256m, 0.055m, "PCS", "KG");
            lines.Rows.Add("Gadget", 10m, 0.9999m, 10.00m, 0.5m, 0.1275m, "PCS", "KG");
            return data;
        }
    }
}
