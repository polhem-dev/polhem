using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Cell-building part of <see cref="GridControl"/>: the interactive, swap and text cells. Lookup cells
    /// are built in `.LookupCells`, in-cell editors in `.CellEditors`, and the text a cell shows is
    /// formatted in `.Formatting`. Split out for file size only; behaviour is unchanged.
    /// </summary>
    public partial class GridControl
    {
        private static bool IsAlwaysOnEditor(ControlType controlType)
            => controlType is ControlType.CheckEdit or ControlType.DropDownEdit
                or ControlType.DateEdit or ControlType.YearMonthEdit;

        // Display template for the popup-based editor columns. Boolean cells render a
        // centred checkbox in every state (a disabled checkbox reads better than
        // "True"/"False" text); the other types rest as plain formatted text and swap
        // in their editor on click — the swap is managed here, not by the DataGrid
        // edit pipeline, so opening the editor's popup cannot tear the editor down.
        private Control BuildInteractiveCell(DataRowView? rowView, LayoutColumn column)
        {
            var canEdit = !_grid.IsReadOnly && !column.ReadOnly && rowView is not null;

            if (column.ControlType == ControlType.CheckEdit)
            {
                CheckBox checkBox;
                if (canEdit)
                {
                    checkBox = (CheckBox)BuildCellEditor(rowView, column);
                }
                else
                {
                    checkBox = new CheckBox { IsChecked = GridCellFormatter.IsChecked(rowView?.Row, column.FieldName), IsEnabled = false };
                }
                checkBox.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
                checkBox.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
                return checkBox;
            }

            if (!canEdit)
            {
                return new TextBlock
                {
                    Text = FormatCellForColumn(rowView, column),
                    Margin = new Thickness(8, 4),
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                };
            }

            // canEdit is true only when rowView is not null (see its definition above),
            // so the swap-cell branch always has a non-null row.
            return BuildSwapCell(rowView!, column);
        }

        // Click-to-edit host: rests as the original text rendering, swaps to the
        // editor on pointer press and swaps back when the edit session ends, re-reading
        // the row so the committed value is what gets displayed.
        private ContentControl BuildSwapCell(DataRowView rowView, LayoutColumn column)
        {
            var host = new ContentControl
            {
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                // Transparent (not null) background: a null background excludes the
                // empty area beside the text from hit-testing, so only the characters
                // themselves would react to the click.
                Background = Brushes.Transparent,
            };

            void ShowDisplay() => host.Content = new TextBlock
            {
                Text = FormatCellForColumn(rowView, column),
                Margin = new Thickness(8, 4),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            };

            host.PointerPressed += (_, _) =>
            {
                if (host.Content is not TextBlock) return;
                // Only one inline editor at a time: starting an edit here closes a
                // lingering editor in another cell (e.g. a dismissed date pick).
                _endActiveInlineEdit?.Invoke();

                var editor = BuildCellEditor(rowView, column);
                editor.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
                Action end = null!;
                end = () =>
                {
                    if (host.Content is TextBlock) return;
                    ShowDisplay();
                    if (ReferenceEquals(_endActiveInlineEdit, end))
                        _endActiveInlineEdit = null;
                };
                WireInlineEditEnd(editor, end);
                host.Content = editor;
                _endActiveInlineEdit = end;
            };

            ShowDisplay();
            return host;
        }

        private static void WireInlineEditEnd(Control editor, Action endEdit)
        {
            switch (editor)
            {
                case ComboBox combo:
                    // Open the dropdown so a single click on the cell goes straight to
                    // picking an option — deferred past the click that swapped the
                    // editor in, otherwise the same pointer interaction closes the
                    // dropdown again immediately. Only a close that follows a real
                    // open ends the edit (guards the same race on the way out).
                    var opened = false;
                    combo.AttachedToVisualTree += (_, _) =>
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            opened = true;
                            combo.IsDropDownOpen = true;
                        });
                    combo.PropertyChanged += (_, e) =>
                    {
                        if (e.Property != ComboBox.IsDropDownOpenProperty) return;
                        if (combo.IsDropDownOpen)
                            opened = true;
                        else if (opened)
                            endEdit();
                    };
                    break;
                case DatePicker picker:
                    // Pop the spinner flyout right away so a single click on the cell
                    // goes straight to picking. `DatePicker` has no public open API.
                    // Raising `Click` on the template's flyout button is the supported
                    // route in. Background priority defers past the layout pass so the
                    // flyout positions against the realized picker.
                    picker.AttachedToVisualTree += (_, _) =>
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            picker.ApplyTemplate();
                            var flyoutButton = global::Avalonia.VisualTree.VisualExtensions
                                .FindDescendantOfType<Button>(picker);
                            flyoutButton?.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                        }, global::Avalonia.Threading.DispatcherPriority.Background);
                    // Commit-driven swap-back only: confirming a date changes
                    // SelectedDate (the write-back hook subscribed first, so the row
                    // is updated before the swap-back re-reads it). LostFocus is NOT
                    // wired — the spinner flyout takes focus and would tear the editor
                    // down mid-pick. A dismissed flyout leaves the editor in place
                    // until the next inline edit or EndEdit closes it.
                    picker.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == DatePicker.SelectedDateProperty)
                            endEdit();
                    };
                    break;
                default:
                    editor.LostFocus += (_, _) => endEdit();
                    break;
            }
        }

        // Builds the in-cell editor for the column's ControlType. The writes go
        // straight to the DataRow (the ADR-020 binding limitation applies to editing
        // templates too); invalid partial input keeps the last valid value.
        /// <summary>
        /// Builds the display cell of a column that edits through the grid's own edit pipeline: the formatted text,
        /// vertically centered like the other cell kinds, and right-aligned for a numeric column.
        /// </summary>
        /// <param name="rowView">The row the cell shows.</param>
        /// <param name="column">The column the cell belongs to.</param>
        /// <returns>The display cell.</returns>
        internal TextBlock BuildTextCell(DataRowView? rowView, LayoutColumn column) => new()
        {
            Text = FormatColumnText(rowView, column),
            Margin = new Thickness(8, 4),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            TextAlignment = IsNumeric(column) ? TextAlignment.Right : TextAlignment.Left,
        };

        private static bool IsNumeric(LayoutColumn column) => column.ControlType == ControlType.NumericEdit;

    }
}
