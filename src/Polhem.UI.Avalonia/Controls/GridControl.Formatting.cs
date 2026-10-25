using System.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

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
            => GridCellFormatter.ComposeDisplayText(rowView?.Row, displayFields, displayFormat, numberFormat);

        private static string FormatCell(DataRowView? row, string fieldName, string displayFormat, string numberFormat)
        {
            if (row is null) return string.Empty;
            var dataRow = row.Row;
            if (!dataRow.Table.Columns.Contains(fieldName)) return string.Empty;
            return FieldValueFormatter.Format(dataRow[fieldName], displayFormat, numberFormat);
        }

        /// <summary>
        /// Formats the text a plain (non-editor) cell of <paramref name="column"/> shows for
        /// <paramref name="row"/>; the rules are <see cref="GridCellFormatter.FormatColumnText"/>, which every
        /// UI head shares.
        /// </summary>
        /// <param name="row">The row to read; <c>null</c> gives an empty string.</param>
        /// <param name="column">The layout column being rendered.</param>
        /// <remarks>
        /// The compact card list of <see cref="Polhem.UI.Avalonia.Views.ListView"/> renders its values
        /// through this method too, so a phone-width list shows a value exactly as the wide grid does.
        /// </remarks>
        internal string FormatColumnText(DataRowView? row, LayoutColumn column)
            => GridCellFormatter.FormatColumnText(row?.Row, column, ResolveFormField(column.FieldName),
                CurrencySettings, DefaultCurrencyCode, UnitSettings, BooleanText);

        private string FormatCellForColumn(DataRowView? row, LayoutColumn column)
            => GridCellFormatter.FormatCell(row?.Row, column, ResolveFormField(column.FieldName),
                CurrencySettings, DefaultCurrencyCode, UnitSettings, BooleanText);

        private static string BooleanText(bool value) => UIText.Get(value ? PolhemUIText.True : PolhemUIText.False);

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
    }
}
