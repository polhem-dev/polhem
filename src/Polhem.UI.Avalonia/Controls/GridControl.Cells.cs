using Polhem.UI.Core;
using System.Data;
using System.Globalization;
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
    /// Cell-building half of <see cref="GridControl"/> (cell rendering, lookup cells and in-cell
    /// editors). The text a cell shows is formatted in `.Formatting`. Split out for file size only;
    /// behaviour is unchanged.
    /// </summary>
    public partial class GridControl
    {
        private static bool IsAlwaysOnEditor(ControlType controlType)
            => controlType is ControlType.CheckEdit or ControlType.DropDownEdit
                or ControlType.DateEdit or ControlType.YearMonthEdit;

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
            var displayFields = SplitDisplayFields(column.DisplayFields);
            if (displayFields.Length == 0)
                displayFields = [.. lookupField.GetDisplayFields()];
            var text = new TextBlock
            {
                Text = ComposeDisplayText(rowView, displayFields, column.DisplayFormat, column.NumberFormat),
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
                // UI boundary: an async pointer handler must not crash the app, so the failure
                // is surfaced as the grid's tooltip.
                ToolTip.SetTip(this, ex.Message);
            }
        }

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
                    var value = string.Equals(
                        FormatCell(rowView, column.FieldName, string.Empty, string.Empty),
                        bool.TrueString, StringComparison.OrdinalIgnoreCase);
                    checkBox = new CheckBox { IsChecked = value, IsEnabled = false };
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
        private Control BuildCellEditor(DataRowView? rowView, LayoutColumn column)
        {
            if (rowView is null) return new TextBlock();
            var fieldName = column.FieldName;
            var dataColumn = rowView.Row.Table.Columns.Contains(fieldName)
                ? rowView.Row.Table.Columns[fieldName]
                : null;
            if (dataColumn is null) return new TextBlock();

            var current = FormatCell(rowView, fieldName, string.Empty, string.Empty);

            if (column.ControlType == ControlType.CheckEdit)
            {
                var checkBox = new CheckBox
                {
                    IsChecked = string.Equals(current, bool.TrueString, StringComparison.OrdinalIgnoreCase),
                };
                checkBox.IsCheckedChanged += (_, _) =>
                    WriteCell(rowView, dataColumn, (checkBox.IsChecked ?? false).ToString());
                return checkBox;
            }

            if (column.ControlType is ControlType.DateEdit or ControlType.YearMonthEdit)
            {
                var format = column.ControlType == ControlType.YearMonthEdit ? "yyyy-MM" : "yyyy-MM-dd";
                var picker = new DatePicker
                {
                    DayVisible = column.ControlType != ControlType.YearMonthEdit,
                    SelectedDate = DateEdit.ParseToOffset(current),
                };
                // `SelectedDateChanged` is not raised reliably for programmatic
                // writes; hook the property change instead (same approach as the
                // TextBox editor).
                picker.PropertyChanged += (_, e) =>
                {
                    if (e.Property == DatePicker.SelectedDateProperty)
                        WriteCell(rowView, dataColumn, picker.SelectedDate?.DateTime.ToString(format, CultureInfo.InvariantCulture));
                };
                return picker;
            }

            if (column.ControlType == ControlType.DateTimeEdit)
                return BuildDateTimeCellEditor(rowView, dataColumn);

            if (column.ControlType == ControlType.DropDownEdit
                && ResolveFormField(fieldName)?.ListItems is { Count: > 0 } items)
            {
                var options = items.ToList();
                var combo = new ComboBox
                {
                    ItemsSource = options,
                    // Same selection-box pitfall as DropDownEdit: a recycling template
                    // starves the collapsed combo of its content instance.
                    DisplayMemberBinding = new global::Avalonia.Data.Binding(
                        nameof(Polhem.Definition.Collections.ListItem.Text)),
                    SelectedItem = options.FirstOrDefault(i => string.Equals(i.Value, current, StringComparison.Ordinal)),
                };
                combo.SelectionChanged += (_, _) =>
                    WriteCell(rowView, dataColumn, (combo.SelectedItem as Polhem.Definition.Collections.ListItem)?.Value);
                return combo;
            }

            return BuildTextCellEditor(rowView, dataColumn, current);
        }

        private static TextBox BuildTextCellEditor(DataRowView rowView, DataColumn dataColumn, string current)
        {
            var textBox = new TextBox { Text = current };
            // Commit on leaving the cell editor (or Enter) rather than per keystroke, matching
            // the standalone TextEdit — so a field's value (and any dependent recalculation)
            // is written once the entry is complete. This LostFocus runs before the swap-back
            // one wired by WireInlineEditEnd (subscribed first), so the row is updated before
            // the display re-reads it. Escape cancels: the grid tears the editor down, and the
            // LostFocus that follows must not write the abandoned text.
            var cancelled = false;
            textBox.LostFocus += (_, _) =>
            {
                if (!cancelled)
                    WriteCell(rowView, dataColumn, textBox.Text);
            };
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                    WriteCell(rowView, dataColumn, textBox.Text);
                else if (e.Key == Key.Escape)
                    cancelled = true;
            };
            return textBox;
        }

        // A text cell enters editing with its content selected, so typing replaces the value the
        // way a spreadsheet does; F2 keeps the caret at the end to amend it. An edit started by
        // typing on a selected cell begins with the typed text. `DataGridTextColumn` does the same
        // for its own editor, but a template column leaves the caret wherever the click put it.
        private static void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is not TextBox textBox) return;
            switch (e.EditingEventArgs)
            {
                case TextInputEventArgs { Text: { } typed }:
                    textBox.Text = typed;
                    textBox.CaretIndex = typed.Length;
                    break;
                case KeyEventArgs { Key: Key.F2 }:
                    textBox.CaretIndex = textBox.Text?.Length ?? 0;
                    break;
                default:
                    textBox.SelectAll();
                    break;
            }
        }

        // Typing on a selected, editable text cell starts editing it with the typed text. The inner
        // grid does not do this on its own; an editor that already has focus handles the input first,
        // so this only sees text typed while no cell is being edited.
        private void OnGridTextInput(object? sender, TextInputEventArgs e)
        {
            if (e.Handled || _grid.IsReadOnly || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
            if (_grid.CurrentColumn is not DataGridTemplateColumn { CellEditingTemplate: not null }) return;
            if (_grid.BeginEdit(e))
                e.Handled = true;
        }

        // The in-cell counterpart of DateTimeEdit: the same culture display and parsing, committed on
        // leaving the cell or on Enter. An unchanged or unparseable text is not written, so the stored
        // value keeps its full precision and a typo never replaces it.
        private static TextBox BuildDateTimeCellEditor(DataRowView rowView, DataColumn dataColumn)
        {
            var raw = rowView.Row[dataColumn];
            var display = raw is DateTime current ? DateTimeFieldFormat.FormatDisplay(current) : string.Empty;
            var textBox = new TextBox { Text = display };

            void Commit()
            {
                var text = textBox.Text ?? string.Empty;
                if (string.Equals(text, display, StringComparison.Ordinal)) return;
                if (string.IsNullOrWhiteSpace(text))
                {
                    WriteCell(rowView, dataColumn, string.Empty);
                }
                else if (DateTimeFieldFormat.TryParseInput(text, out var value))
                {
                    WriteCell(rowView, dataColumn, DateTimeFieldFormat.ToBindingValue(value));
                }
                else
                {
                    return;
                }
                display = text;
            }

            textBox.LostFocus += (_, _) => Commit();
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                    Commit();
            };
            return textBox;
        }

        private static void WriteCell(DataRowView rowView, DataColumn column, string? value)
        {
            if (!TryConvertCellValue(value, column, out var converted)) return;
            if (Equals(converted, rowView.Row[column])) return;
            // The write raises FieldValueChanged and marks dirty through the data
            // object's DataTable event bridge.
            rowView.Row[column] = converted;
        }

        private static bool TryConvertCellValue(string? value, DataColumn column, out object converted)
        {
            try
            {
                converted = FormValueBinding.ToColumnValue(value, column);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                // Partial keystroke input (for example "12." in a decimal column)
                // cannot convert yet; the cell keeps its last valid value until the
                // input parses.
                converted = DBNull.Value;
                return false;
            }
        }

        private void OnSelectionChangedCore(object? sender, SelectionChangedEventArgs e)
        {
            var handler = RowSelected;
            if (handler is null) return;
            if (_grid.SelectedItem is not DataRowView rowView) return;
            if (!FormDataGuard.TryGetRowId(rowView.Row, out var rowId)) return;
            handler(this, rowId);
        }

        private static IEnumerable<LayoutColumn> EnumerateVisibleColumns(LayoutGrid layout)
            => layout.Columns?.Where(c => c.Visible) ?? Enumerable.Empty<LayoutColumn>();

    }
}
