using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.DemoCenter.Modules.DataEditors;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.DataObjects;
using Polhem.UI.Core.Permissions;

namespace Avalonia.DemoCenter.Modules.Permissions
{
    /// <summary>
    /// Interactive front-end permission (capability) simulator over a master-detail form. The left
    /// panel fakes the role grants that <c>EnterCompany</c> would normally return; the right panel is
    /// a mock purchase order (master fields + a real detail <see cref="GridControl"/>) whose toolbar
    /// commands and sensitive fields degrade live through the <em>real</em>
    /// <see cref="ElementCapabilityResolver"/> — no back end involved.
    /// </summary>
    /// <remarks>
    /// The capability snapshot is just a <c>Dictionary&lt;modelId, PermissionAction&gt;</c>, so a demo
    /// can fabricate it in memory and exercise the exact same resolver the shipped views use. Field
    /// permission spans both tables: the master's <c>Handler ID number</c> (PersonalData) and the detail
    /// grid's <c>Unit price</c> (Cost) column each degrade by their category — the resolver reverse-looks-up
    /// the <see cref="FormField.SensitiveCategory"/> from the schema by (table, field). UX only; in a
    /// real app the snapshot comes from the server and the back end stays authoritative.
    /// </remarks>
    public sealed class PermissionCapabilityModule : DemoModuleBase
    {
        /// <inheritdoc/>
        public override string Category => "Permission Capability";

        /// <inheritdoc/>
        public override string Title => "Interactive permission simulator (master/detail)";

        /// <inheritdoc/>
        public override string Description =>
            "Tick the boxes on the left to simulate role grants (the same as the capability snapshot EnterCompany returns); the Purchase Order master and detail on the right degrade live: "
            + "toolbar commands without permission are hidden; the sensitive master field (Handler ID number = PersonalData) is hidden, read-only or editable according to Read/Update; "
            + "the sensitive detail grid column (Unit price = Cost) is hidden entirely without Read. Everything is granted by default (the full form); "
            + "untick a box to watch the matching element degrade live. Turning off Enable capability makes the snapshot null, which allows everything.";

        private const string FormModel = "PurchaseOrder";
        private const string CostModel = "Cost";
        private const string PiiModel = "PersonalData";
        private const string MasterTable = "PO001";
        private const string DetailTable = "PO001_Item";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var schema = BuildSchema();
            var detailData = BuildData(schema);

            // ---- Right: master fields + a real detail grid ----
            var newBtn = MakeCommand("New", PermissionAction.Create);
            var saveBtn = MakeCommand("Save", PermissionAction.Create | PermissionAction.Update);
            var deleteBtn = MakeCommand("Delete", PermissionAction.Delete);
            var viewBtn = MakeCommand("View", PermissionAction.Read);
            var commands = new[] { newBtn, saveBtn, deleteBtn, viewBtn };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { newBtn, saveBtn, deleteBtn, viewBtn } };

            var idField = MakeField("sys_id", "Order No.");
            var vendorField = MakeField("vendor", "Vendor");
            var handlerField = MakeField("handler_id", "Handler ID number");   // SensitiveCategory=PersonalData
            var masterFields = new[] { idField, vendorField, handlerField };

            var masterBody = new StackPanel { Spacing = 10, Children = { toolbar, idField.Row, vendorField.Row, handlerField.Row } };
            var detailHost = new Border { MinHeight = 150 };   // holds the (re)built detail grid

            var formBody = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    DataEditorParts.Section("Master PO001 (PermissionModelId = PurchaseOrder)", null, masterBody),
                    DataEditorParts.Section("Detail PO001_Item (GridControl, Unit price column = Cost)", null, detailHost),
                },
            };

            // ---- Left: simulated grant toggles ----
            var active = new CheckBox { Content = "Enable capability (otherwise the snapshot is null → everything allowed)", IsChecked = true };

            // Everything granted by default → the full form shows on open; uncheck a grant to
            // watch that command / field degrade live.
            var poCreate = Grant("Create", on: true); var poRead = Grant("Read", on: true);
            var poUpdate = Grant("Update", on: true); var poDelete = Grant("Delete", on: true);
            var costRead = Grant("Read", on: true); var costUpdate = Grant("Update", on: true);
            var piiRead = Grant("Read", on: true); var piiUpdate = Grant("Update", on: true);

            var grantsPanel = new StackPanel
            {
                Spacing = 12,
                MinWidth = 260,
                Children =
                {
                    active,
                    GrantSection("Purchase Order model (PurchaseOrder) — drives the toolbar commands", poCreate, poRead, poUpdate, poDelete),
                    GrantSection("Personal data category (PersonalData) — drives the master ID number field", piiRead, piiUpdate),
                    GrantSection("Cost category (Cost) — drives the detail Unit price column", costRead, costUpdate),
                },
            };

            void Refresh()
            {
                IReadOnlyDictionary<string, PermissionAction>? snapshot = active.IsChecked == true
                    ? new Dictionary<string, PermissionAction>(StringComparer.Ordinal)
                    {
                        [FormModel] = Mask((poCreate, PermissionAction.Create), (poRead, PermissionAction.Read), (poUpdate, PermissionAction.Update), (poDelete, PermissionAction.Delete)),
                        [PiiModel] = Mask((piiRead, PermissionAction.Read), (piiUpdate, PermissionAction.Update)),
                        [CostModel] = Mask((costRead, PermissionAction.Read), (costUpdate, PermissionAction.Update)),
                    }
                    : null; // null snapshot = capability inactive = allow all (the resolver's safe default).

                // Commands: each button carries the action it needs (PermissionScope); hide it when not permitted.
                foreach (var btn in commands)
                    btn.IsVisible = ElementCapabilityResolver.Default.Can(schema, PermissionScope.GetAction(btn), snapshot);

                // Master fields: sensitive ones degrade by SensitiveCategory (table = master).
                foreach (var f in masterFields)
                {
                    var cap = ElementCapabilityResolver.Default.ResolveField(schema, f.FieldName, tableName: MasterTable, snapshot);
                    f.Row.IsVisible = cap.Visible;
                    f.Editor.IsReadOnly = cap.ReadOnly;
                    f.Editor.Opacity = cap.ReadOnly ? 0.55 : 1.0;
                }

                // Detail grid: rebuild from a fresh layout with capability applied to its columns
                // (table = detail). This mirrors LayoutCapabilityApplier, which is internal to the lib.
                detailHost.Child = BuildDetailGrid(schema, detailData, snapshot);
            }

            active.IsCheckedChanged += (_, _) => Refresh();
            foreach (var cb in new[] { poCreate, poRead, poUpdate, poDelete, costRead, costUpdate, piiRead, piiUpdate })
                cb.IsCheckedChanged += (_, _) => Refresh();
            Refresh();

            // Two bounded columns (grants | form) so the detail DataGrid gets a real width and lays
            // out all its columns, instead of being starved inside an unbounded horizontal stack.
            var root = new Grid { Margin = new Thickness(4), ColumnSpacing = 24 };
            root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var grantsSection = DataEditorParts.Section("Simulated grants (capability snapshot)", null, grantsPanel);
            Grid.SetColumn(grantsSection, 0);
            Grid.SetColumn(formBody, 1);
            root.Children.Add(grantsSection);
            root.Children.Add(formBody);
            return new ScrollViewer { Content = root };
        }

        // Master PO001 (with a PersonalData field) + detail PO001_Item (with a Cost column).
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(MasterTable, "Purchase Order") { PermissionModelId = FormModel };

            var master = schema.Tables!.Add(MasterTable, "Purchase Order");
            master.Fields!.Add("sys_id", "Order No.", FieldDbType.String);
            master.Fields!.Add("vendor", "Vendor", FieldDbType.String);
            master.Fields!.Add("handler_id", "Handler ID number", FieldDbType.String).SensitiveCategory = SensitiveCategory.PersonalData;

            var detail = schema.Tables.Add(DetailTable, "Items");
            detail.Fields!.Add("item_name", "Item", FieldDbType.String);
            detail.Fields!.Add("qty", "Quantity", FieldDbType.Integer);
            detail.Fields!.Add("unit_cost", "Unit price", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;

            return schema;
        }

        private static FormDataObject BuildData(FormSchema schema)
        {
            var data = new FormDataObject(schema);
            data.InitializeNewMaster();
            data.SetField("sys_id", "PO-2026-001");
            data.SetField("vendor", "Acer Inc.");
            data.SetField("handler_id", "A123456789");

            var items = data.DataSet.Tables[DetailTable]!;
            items.Rows.Add("Screw M4", 100, 3.5m);
            items.Rows.Add("Washer 8mm", 50, 1.2m);
            return data;
        }

        // Builds a fresh detail grid each refresh: a fresh layout with capability applied to its
        // columns (narrowing only), then bound. Fresh layout means re-granting Cost.Read re-shows the column.
        private static Control BuildDetailGrid(FormSchema schema, FormDataObject data, IReadOnlyDictionary<string, PermissionAction>? snapshot)
        {
            var layout = new LayoutGrid(DetailTable, "Items");
            layout.Columns!.Add(new LayoutColumn("item_name", "Item", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("qty", "Quantity", ControlType.TextEdit));
            layout.Columns.Add(new LayoutColumn("unit_cost", "Unit price", ControlType.TextEdit));

            foreach (var column in layout.Columns)
            {
                var cap = ElementCapabilityResolver.Default.ResolveField(schema, column.FieldName, DetailTable, snapshot);
                if (!cap.Visible) { column.Visible = false; }
                if (cap.ReadOnly) { column.ReadOnly = true; }
            }

            var grid = new GridControl { MinHeight = 140 };
            grid.Bind(data, layout);
            return grid;
        }

        private static Button MakeCommand(string text, PermissionAction action)
        {
            var button = new Button { Content = text };
            PermissionScope.SetAction(button, action);   // the same tagging the shipped views use
            return button;
        }

        private static FieldRow MakeField(string fieldName, string caption)
        {
            var editor = new TextBox { MinWidth = 200 };
            var row = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = caption, Opacity = 0.7 },
                    editor,
                },
            };
            return new FieldRow(fieldName, row, editor);
        }

        private static CheckBox Grant(string action, bool on = false) => new() { Content = action, IsChecked = on };

        private static Border GrantSection(string title, params Control[] toggles)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            foreach (var t in toggles) stack.Children.Add(t);
            return DataEditorParts.Section(title, null, stack);
        }

        private static PermissionAction Mask(params (CheckBox toggle, PermissionAction action)[] items)
        {
            var mask = PermissionAction.None;
            foreach (var (toggle, action) in items)
                if (toggle.IsChecked == true) { mask |= action; }
            return mask;
        }

        // Holds a field's caption+editor row together so capability can toggle both.
        private sealed class FieldRow(string fieldName, Control row, TextBox editor)
        {
            public string FieldName { get; } = fieldName;
            public Control Row { get; } = row;
            public TextBox Editor { get; } = editor;
        }
    }
}
