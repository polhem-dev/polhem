using System.Data;
using Polhem.Api.Client;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Value-formatting part of <see cref="GridControl"/>: the text a cell and the compact card list
    /// show for a stored value, in the user's culture, with per-row currency and unit decimals, and with
    /// drop-down codes, Booleans and instants turned into what a person reads.
    /// </summary>
    public partial class GridControl
    {
        private static string[] SplitDisplayFields(string displayFields)
            => string.IsNullOrEmpty(displayFields)
                ? []
                : displayFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Joins the non-empty display-field values (e.g. "D001 - Engineering").
        private static string ComposeDisplayText(
            DataRowView? rowView, string[] displayFields, string displayFormat, string numberFormat)
        {
            if (displayFields.Length == 0) return string.Empty;
            return LookupDisplay.Compose(displayFields
                .Select(f => FormatCell(rowView, f, displayFormat, numberFormat)));
        }

        private static string FormatCell(DataRowView? row, string fieldName, string displayFormat, string numberFormat)
        {
            if (row is null) return string.Empty;
            var dataRow = row.Row;
            if (!dataRow.Table.Columns.Contains(fieldName)) return string.Empty;
            return CellValueFormatter.Format(dataRow[fieldName], displayFormat, numberFormat);
        }

        /// <summary>
        /// Formats the text a plain (non-editor) cell of <paramref name="column"/> shows for
        /// <paramref name="row"/>: the composed display fields of a list-mode lookup column instead of
        /// its raw row id, otherwise the value in the user's culture with the column's delivered or
        /// currency/unit-resolved number format.
        /// </summary>
        /// <param name="row">The row to read; <c>null</c> gives an empty string.</param>
        /// <param name="column">The layout column being rendered.</param>
        /// <remarks>
        /// The compact card list of <see cref="Polhem.UI.Avalonia.Views.ListView"/> renders its values
        /// through this method too, so a phone-width list shows a value exactly as the wide grid does.
        /// </remarks>
        internal string FormatColumnText(DataRowView? row, LayoutColumn column)
        {
            var textFields = SplitDisplayFields(column.DisplayFields);
            return textFields.Length == 0
                ? FormatCellForColumn(row, column)
                : ComposeDisplayText(row, textFields, column.DisplayFormat, column.NumberFormat);
        }

        // Currency-aware cell text: an Amount column resolves its decimals per row from the referenced
        // currency (see ResolveCellNumberFormat); every other column uses the column's delivered formats.
        //
        // Values whose stored form is not what a person reads are translated first: a drop-down column
        // shows its list item's text (localized when the schema was), an instant column always shows its
        // time of day, and a Boolean outside a check-box column reads as the localized Yes / No.
        private string FormatCellForColumn(DataRowView? row, LayoutColumn column)
        {
            if (row is null) return string.Empty;
            var dataRow = row.Row;
            if (!dataRow.Table.Columns.Contains(column.FieldName)) return string.Empty;
            var raw = dataRow[column.FieldName];

            if (column.ControlType == ControlType.DropDownEdit
                && FormValueBinding.TryGetListItemText(raw, ResolveFormField(column.FieldName)?.ListItems, out var itemText))
            {
                return itemText;
            }
            if (string.IsNullOrEmpty(column.DisplayFormat))
            {
                if (column.ControlType == ControlType.DateTimeEdit && raw is DateTime instant)
                    return DateTimeEdit.FormatDisplay(instant);
                if (raw is bool flag)
                    return UIText.Get(flag ? PolhemUIText.True : PolhemUIText.False);
            }

            string numberFormat = ResolveCellNumberFormat(dataRow, column);
            return CellValueFormatter.Format(raw, column.DisplayFormat, numberFormat);
        }

        // The schema field behind a column: through the bound data object for a detail grid, through the
        // table supplied with a list-mode bind otherwise; null when neither knows the field.
        private FormField? ResolveFormField(string fieldName)
        {
            var dataObject = _binder.DataObject;
            if (dataObject is not null)
                return dataObject.GetFormField(TableName, fieldName);
            var fields = _listFormTable?.Fields;
            return fields is not null && fields.Contains(fieldName) ? fields[fieldName] : null;
        }

        // Reference-bound columns (amounts by currency, quantities/weights by unit) are not baked at
        // delivery — resolve their format from the row's reference value and the client master. Other
        // kinds (and the no-master / no-reference cases) keep the delivered format.
        private string ResolveCellNumberFormat(DataRow dataRow, LayoutColumn column)
        {
            var source = NumberKindProfile.GetDecimalsSource(column.NumberKind);

            if (source == DecimalsSource.Currency && CurrencySettings is not null)
            {
                string code = ResolveCellReferenceCode(dataRow, column.CurrencyField, DefaultCurrencyCode);
                return NumberFormatResolver.ResolveFormat(
                    column.NumberKind, new RoundingContext { CurrencySettings = CurrencySettings }, code);
            }

            if (source == DecimalsSource.Unit && UnitSettings is not null)
            {
                string code = ResolveCellReferenceCode(dataRow, column.UnitField, string.Empty);
                if (StringUtilities.IsNotEmpty(code))
                    return NumberFormatResolver.ResolveFormat(
                        column.NumberKind, new RoundingContext { UnitSettings = UnitSettings }, code);
            }

            return column.NumberFormat;
        }

        // Per-row reference code: the column's reference field (currency key / unit) on this row →
        // the supplied fallback. Empty resolves to the framework fallback downstream.
        private static string ResolveCellReferenceCode(DataRow dataRow, string referenceField, string fallback)
        {
            if (StringUtilities.IsNotEmpty(referenceField)
                && dataRow.Table.Columns.Contains(referenceField))
            {
                string rowCode = ValueUtilities.CStr(dataRow[referenceField]);
                if (StringUtilities.IsNotEmpty(rowCode)) { return rowCode; }
            }
            return fallback;
        }
    }
}
