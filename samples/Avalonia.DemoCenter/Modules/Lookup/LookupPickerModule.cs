using Avalonia.Controls;
using Avalonia.Layout;
using Polhem.Core.Data;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Avalonia.DataObjects;
using Avalonia.DemoCenter.Modules.DataEditors;

namespace Avalonia.DemoCenter.Modules.Lookup
{
    /// <summary>
    /// Open-window data picking: a <see cref="ButtonEdit"/> whose icon opens a picker window;
    /// the selection is written back through the data object.
    /// </summary>
    /// <remarks>
    /// This demo center has no backend, so the icon raises <see cref="ButtonEdit.ButtonClick"/>
    /// and a local picker stands in. The production lookup flow (<c>FormField.RelationProgId</c>
    /// → <c>LookupDialog</c> resolving against the backend, writing the row id + mapped fields)
    /// is shown end-to-end in <c>apps/Polhem.Northwind</c>.
    /// </remarks>
    public sealed class LookupPickerModule : DemoModuleBase
    {
        private static readonly string[] s_codes = ["EMP-001", "EMP-002", "EMP-003", "EMP-004"];

        /// <inheritdoc/>
        public override string Category => "Lookup";

        /// <inheritdoc/>
        public override string Title => "ButtonEdit lookup picker";

        /// <inheritdoc/>
        public override string Description =>
            "Click the magnifier icon on the right of the ButtonEdit to open a picker; the selection is written back (a local picker). "
            + "The production RelationProgId → LookupDialog back-end query flow is in apps/Polhem.Northwind.";

        /// <inheritdoc/>
        public override Control BuildView()
        {
            var data = DataEditorParts.SingleField("code", "Code", FieldDbType.String, initialValue: "EMP-001");

            var bound = new ButtonEdit { FieldName = "code" };
            bound.ButtonClick += (_, _) => _ = ShowPickerAsync(bound, data);

            var readOnly = new ButtonEdit();
            readOnly.Bind(data, new LayoutField { FieldName = "code", ReadOnly = true });

            return DataEditorParts.Compose(
                data,
                DataEditorParts.Section(
                    "Lookup picker",
                    "Click the magnifier on the right to open the picker; the selected value is written back immediately (the icon is hidden when read-only — see the read-only section below and the FormMode States theme).",
                    DataEditorParts.LabeledRow("code", bound),
                    DataEditorParts.LiveValue(data, "code")),
                DataEditorParts.Section(
                    "Read-only (icon hidden)",
                    "With ReadOnly=true the icon is hidden and only the value is shown.",
                    DataEditorParts.LabeledRow("code", readOnly)));
        }

        // A minimal in-app picker standing in for the backend lookup dialog: pick a code from
        // a fixed list and write it back through the data object.
        private static async Task ShowPickerAsync(Control anchor, FormDataObject data)
        {
            if (TopLevel.GetTopLevel(anchor) is not Window owner)
                return;

            var list = new ListBox
            {
                ItemsSource = s_codes,
                SelectedItem = data.GetField("code"),
                Margin = new Thickness(8),
            };

            string? result = null;
            var ok = new Button { Content = "Select", IsDefault = true, MinWidth = 72 };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 72 };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(8),
                Children = { ok, cancel },
            };
            DockPanel.SetDock(buttons, Dock.Bottom);

            var dialog = new Window
            {
                Title = "Choose a code",
                Width = 260,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new DockPanel { Children = { buttons, list } },
            };

            ok.Click += (_, _) => { result = list.SelectedItem as string; dialog.Close(); };
            cancel.Click += (_, _) => dialog.Close();

            await dialog.ShowDialog(owner);

            if (result is not null)
                data.SetField("code", result);
        }
    }
}
