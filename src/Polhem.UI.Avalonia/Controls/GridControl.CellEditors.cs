using Polhem.UI.Core;
using System.Data;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// In-cell editors of <see cref="GridControl"/>: building the editor for a column, and converting and
    /// writing what was typed back to the row. Split out for file size only; behaviour is unchanged.
    /// </summary>
    public partial class GridControl
    {
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
                    IsChecked = GridCellFormatter.IsChecked(rowView.Row, fieldName),
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

            var textEditor = BuildTextCellEditor(rowView, dataColumn, current);
            if (IsNumeric(column))
                textEditor.TextAlignment = TextAlignment.Right;
            return textEditor;
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
    }
}
