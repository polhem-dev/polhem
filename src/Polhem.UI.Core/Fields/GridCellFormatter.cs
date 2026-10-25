using System.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// The text a grid cell shows for a stored value, the same in every UI head: in the user's culture,
    /// with per-row currency and unit decimals, the composed display fields of a lookup column, and
    /// drop-down codes, Booleans and instants turned into what a person reads.
    /// </summary>
    public static class GridCellFormatter
    {
        /// <summary>
        /// Formats the text a plain (non-editor) cell of <paramref name="column"/> shows for
        /// <paramref name="row"/>: the composed display fields when the column declares
        /// <see cref="LayoutFieldBase.DisplayFields"/> (a lookup column shows those instead of its raw row id),
        /// otherwise <see cref="FormatCell"/>.
        /// </summary>
        /// <param name="row">The row to read; <c>null</c> gives an empty string.</param>
        /// <param name="column">The layout column being rendered.</param>
        /// <param name="formField">The schema field behind the column, or <c>null</c>.</param>
        /// <param name="currencySettings">The currency master, or <c>null</c> to keep the delivered amount formats.</param>
        /// <param name="defaultCurrencyCode">The currency code used when the row carries none.</param>
        /// <param name="unitSettings">The unit-of-measure master, or <c>null</c> to keep the delivered formats.</param>
        /// <param name="booleanText">Returns the localized text for a Boolean value.</param>
        /// <returns>The cell text.</returns>
        public static string FormatColumnText(DataRow? row, LayoutColumn column, FormField? formField,
            CurrencySettings? currencySettings, string defaultCurrencyCode, UnitSettings? unitSettings,
            Func<bool, string> booleanText)
        {
            ArgumentNullException.ThrowIfNull(column);
            var displayFields = SplitDisplayFields(column.DisplayFields);
            return displayFields.Length == 0
                ? FormatCell(row, column, formField, currencySettings, defaultCurrencyCode, unitSettings, booleanText)
                : ComposeDisplayText(row, displayFields, column.DisplayFormat, column.NumberFormat);
        }

        /// <summary>
        /// Formats the value of <paramref name="column"/> on <paramref name="row"/>: a drop-down column shows
        /// its list item's text, an instant column always shows its time of day, a Boolean outside a
        /// check-box column reads as <paramref name="booleanText"/>, and a number uses the column's delivered
        /// format, or the currency or unit decimals of the row's reference value when the master is given.
        /// </summary>
        /// <param name="row">The row to read; <c>null</c> gives an empty string.</param>
        /// <param name="column">The layout column being rendered.</param>
        /// <param name="formField">The schema field behind the column, or <c>null</c>.</param>
        /// <param name="currencySettings">The currency master, or <c>null</c> to keep the delivered amount formats.</param>
        /// <param name="defaultCurrencyCode">The currency code used when the row carries none.</param>
        /// <param name="unitSettings">The unit-of-measure master, or <c>null</c> to keep the delivered formats.</param>
        /// <param name="booleanText">Returns the localized text for a Boolean value.</param>
        /// <returns>The cell text.</returns>
        public static string FormatCell(DataRow? row, LayoutColumn column, FormField? formField,
            CurrencySettings? currencySettings, string defaultCurrencyCode, UnitSettings? unitSettings,
            Func<bool, string> booleanText)
        {
            ArgumentNullException.ThrowIfNull(column);
            ArgumentNullException.ThrowIfNull(booleanText);
            if (row is null || !row.Table.Columns.Contains(column.FieldName)) return string.Empty;
            var raw = row[column.FieldName];

            if (column.ControlType == ControlType.DropDownEdit
                && FormValueBinding.TryGetListItemText(raw, formField?.ListItems, out var itemText))
            {
                return itemText;
            }
            if (string.IsNullOrEmpty(column.DisplayFormat))
            {
                if (column.ControlType == ControlType.DateTimeEdit && raw is DateTime instant)
                    return DateTimeFieldFormat.FormatDisplay(instant);
                if (raw is bool flag)
                    return booleanText(flag);
            }

            // A unit-bound column falls back to its delivered format when the row has no unit, so no
            // default unit code is passed here.
            string numberFormat = NumericFieldFormat.ResolveFormat(
                column, formField: null, row, currencySettings, defaultCurrencyCode, unitSettings, string.Empty);
            return FieldValueFormatter.Format(raw, column.DisplayFormat, numberFormat);
        }

        /// <summary>
        /// Joins the non-empty values of <paramref name="displayFields"/> on <paramref name="row"/>
        /// (e.g. "D001 - Engineering"), each formatted in the user's culture.
        /// </summary>
        /// <param name="row">The row to read; <c>null</c> gives an empty string.</param>
        /// <param name="displayFields">The field names to show, in order.</param>
        /// <param name="displayFormat">An explicit display format string, or empty.</param>
        /// <param name="numberFormat">The numeric format string, or empty.</param>
        /// <returns>The display text.</returns>
        public static string ComposeDisplayText(DataRow? row, IReadOnlyList<string> displayFields,
            string displayFormat, string numberFormat)
        {
            ArgumentNullException.ThrowIfNull(displayFields);
            if (row is null || displayFields.Count == 0) return string.Empty;
            return LookupDisplay.Compose(displayFields.Select(f => row.Table.Columns.Contains(f)
                ? FieldValueFormatter.Format(row[f], displayFormat, numberFormat)
                : string.Empty));
        }

        private static string[] SplitDisplayFields(string displayFields)
            => string.IsNullOrEmpty(displayFields)
                ? []
                : displayFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
