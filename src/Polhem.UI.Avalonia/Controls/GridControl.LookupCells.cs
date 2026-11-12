using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Lookup cells of <see cref="GridControl"/>: the cell that shows a related record and opens it or its
    /// lookup. Split out for file size only; behaviour is unchanged.
    /// </summary>
    public partial class GridControl
    {
        /// <summary>
        /// Resolves the relation metadata for a lookup column: a
        /// <see cref="ControlType.ButtonEdit"/> column whose schema field carries
        /// <c>RelationProgId</c> on the bound table. Returns <c>null</c> in list mode
        /// (no data object) — lookup editing needs the data object write path.
        /// </summary>
        private FormField? TryGetLookupFormField(LayoutColumn column)
        {
            if (column.ControlType != ControlType.ButtonEdit) return null;
            var dataObject = _binder.DataObject;
            var tableName = TableName;
            if (dataObject is null || string.IsNullOrEmpty(tableName)) return null;
            var field = dataObject.GetFormField(tableName, column.FieldName);
            return field is not null && !string.IsNullOrEmpty(field.RelationProgId) ? field : null;
        }

        // Lookup cells rest as the composed display-field text — typically
        // "<id> <name>", id only for transactional targets, empty when no display
        // field resolves (never the raw Guid); a click on an editable cell opens
        // the lookup dialog and the selection writes back through the data object.
        private Control BuildLookupCell(DataRowView? rowView, LayoutColumn column, FormField lookupField)
        {
            var displayFields = LookupFieldRules.ResolveDisplayFieldNames(column, lookupField);
            var text = new TextBlock
            {
                Text = GridCellFormatter.ComposeDisplayText(rowView?.Row, displayFields, column.DisplayFormat, column.NumberFormat),
                Margin = new Thickness(8, 4),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            };

            var canEdit = !_grid.IsReadOnly && !column.ReadOnly && rowView is not null;
            if (!canEdit) return OffersRelatedRecord(rowView, lookupField, out var target) ? BuildOpenRecordCell(text, target) : text;

            // Mirror the standalone ButtonEdit: an editable lookup cell shows a trailing
            // magnifier icon as the "opens a dialog" cue. The text fills the remaining
            // width so the icon stays pinned to the right edge.
            text.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
            var icon = BuildLookupCellIcon();
            DockPanel.SetDock(icon, Dock.Right);
            var content = new DockPanel { LastChildFill = true };
            content.Children.Add(icon);
            content.Children.Add(text);

            // Transparent (not null) background: a null background excludes the empty
            // area beside the text from hit-testing.
            var host = new Border
            {
                Background = Brushes.Transparent,
                Child = content,
            };
            host.PointerPressed += async (_, e) =>
            {
                e.Handled = true;
                // Only one inline editor at a time: opening the dialog closes a
                // lingering editor in another cell.
                _endActiveInlineEdit?.Invoke();
                await OpenLookupCellAsync(rowView!.Row, lookupField).ConfigureAwait(true);
            };
            return host;
        }

        /// <summary>
        /// Resolves the record a double-tap on a lookup cell opens: a value in the row, in View mode, inside a form that
        /// handles <see cref="RelationOpening.RequestedEvent"/>, as a relation field's <see cref="ButtonEdit"/> decides.
        /// </summary>
        internal bool OffersRelatedRecord(DataRowView? rowView, FormField lookupField, out RelationTarget target)
        {
            target = default;
            return rowView is not null
                && rowView.Row.Table.Columns.Contains(lookupField.FieldName)
                && RelationOpening.GetIsEnabled(this)
                && RelationOpenRules.IsAvailable(GetValue(FormScope.FormModeProperty), lookupField)
                && RelationOpenRules.TryGetTarget(lookupField, rowView.Row[lookupField.FieldName], out target);
        }

        // A read-only lookup cell that opens the record it points at: the hint and the hand cursor, and a double-tap that
        // bubbles the request up to the view. The cursor needs platform services, so it is set on first visual attach.
        private static Border BuildOpenRecordCell(TextBlock text, RelationTarget target)
        {
            // Transparent (not null) background: a null background excludes the empty area beside the text from
            // hit-testing.
            var host = new Border { Background = Brushes.Transparent, Child = text };
            ToolTip.SetTip(host, UIText.Get(PolhemUIText.OpenRelatedRecord));
            host.AttachedToVisualTree += (_, _) => host.Cursor ??= new Cursor(StandardCursorType.Hand);
            host.DoubleTapped += (_, e) =>
            {
                e.Handled = true;
                host.RaiseEvent(new RelationOpenRequestedEventArgs(target));
            };
            return host;
        }

        // Trailing magnifier icon for an editable lookup cell, matching the muted tone
        // of the standalone ButtonEdit's icon. `Geometry.Parse` and `Cursor` both need
        // platform services that are absent when unit tests build cells without an
        // Avalonia platform, so they are created on first visual attach instead.
        private static PathIcon BuildLookupCellIcon()
        {
            var icon = new PathIcon
            {
                Width = 14,
                Height = 14,
                Opacity = 0.65,
                Margin = new Thickness(4, 0, 8, 0),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            };
            icon.AttachedToVisualTree += (_, _) =>
            {
                icon.Data ??= Geometry.Parse(LookupIconGeometry);
                icon.Cursor ??= new Cursor(StandardCursorType.Hand);
            };
            return icon;
        }

        private async Task OpenLookupCellAsync(DataRow row, FormField lookupField)
        {
            var dataObject = _binder.DataObject;
            if (dataObject is null) return;

            var progId = lookupField.RelationProgId;
            try
            {
                var selected = await LookupDialog.ShowAsync(this, progId).ConfigureAwait(true);
                if (selected is null) return;
                dataObject.ApplyLookupSelection(lookupField, selected, row);
                // Realized text cells capture their value when the template is built, so the
                // grid is re-realized and scrolled back to the affected row, the same as after a
                // committed edit form.
                RefreshAndFocusRow(row);
            }
            catch (Exception ex)
            {
                // Boundary: an async pointer handler must not crash the app, so the failure
                // is surfaced as the grid's tooltip.
                ToolTip.SetTip(this, ex.Message);
            }
        }
    }
}
