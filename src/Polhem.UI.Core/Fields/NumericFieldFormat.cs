using System.Data;
using System.Globalization;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Format resolution, display and input rules for a numeric field, shared by the numeric editors of
    /// every UI head.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Display and input follow <see cref="CultureInfo.CurrentCulture"/>, the signed-in user's culture:
    /// a <c>de-DE</c> user sees and types <c>1,5</c>, an <c>en-US</c> user <c>1.5</c>. The bound value
    /// stays in the invariant form the wire carries, whatever the user's culture.
    /// </para>
    /// <para>
    /// Input accepts a sign and the culture's decimal separator only, not group separators. A group
    /// separator is the other culture's decimal separator, so accepting it would read a <c>de-DE</c>
    /// user's <c>1,5</c> as fifteen under <c>en-US</c> rules; rejecting it lets the editor keep the last
    /// valid value instead of silently writing a different number.
    /// </para>
    /// </remarks>
    public static class NumericFieldFormat
    {
        private const NumberStyles InputStyles =
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        /// <summary>
        /// Resolves the display format of a numeric field.
        /// </summary>
        /// <remarks>
        /// Reference-bound fields (amounts by currency, quantities and weights by unit) are not baked at
        /// delivery: when the matching master is supplied, the format is resolved at runtime from the
        /// row's reference value, then the fallback code. Otherwise the delivered layout format (baked per
        /// company) wins, falling back to the schema field's format for binds that carry no layout field.
        /// </remarks>
        /// <param name="layoutField">The layout field or grid column, or <c>null</c>.</param>
        /// <param name="formField">The schema field, or <c>null</c>.</param>
        /// <param name="row">The row whose reference field supplies the currency or unit code, or <c>null</c>.</param>
        /// <param name="currencySettings">The currency master, or <c>null</c> to turn currency awareness off.</param>
        /// <param name="defaultCurrencyCode">The currency code used when the row carries none.</param>
        /// <param name="unitSettings">The unit-of-measure master, or <c>null</c> to turn unit awareness off.</param>
        /// <param name="defaultUnitCode">The unit code used when the row carries none.</param>
        /// <returns>The .NET numeric format string, or empty when the field has none.</returns>
        public static string ResolveFormat(
            LayoutFieldBase? layoutField,
            FormField? formField,
            DataRow? row,
            CurrencySettings? currencySettings,
            string defaultCurrencyCode,
            UnitSettings? unitSettings,
            string defaultUnitCode)
        {
            if (layoutField is not null)
            {
                var source = NumberKindProfile.GetDecimalsSource(layoutField.NumberKind);
                if (source == DecimalsSource.Currency && currencySettings is not null)
                    return NumberFormatResolver.ResolveFormat(
                        layoutField.NumberKind,
                        new RoundingContext { CurrencySettings = currencySettings },
                        ResolveReferenceCode(row, layoutField.CurrencyField, defaultCurrencyCode));

                if (source == DecimalsSource.Unit && unitSettings is not null)
                {
                    string code = ResolveReferenceCode(row, layoutField.UnitField, defaultUnitCode);
                    if (StringUtilities.IsNotEmpty(code))
                        return NumberFormatResolver.ResolveFormat(
                            layoutField.NumberKind, new RoundingContext { UnitSettings = unitSettings }, code);
                }
            }

            return layoutField?.NumberFormat is { Length: > 0 } layoutFormat
                ? layoutFormat
                : formField?.NumberFormat ?? string.Empty;
        }

        /// <summary>
        /// Formats a bound value for display at rest with <paramref name="numberFormat"/>; without a format
        /// it shows the editing form. A value that does not parse is shown as is.
        /// </summary>
        /// <param name="raw">The bound value, in the invariant culture.</param>
        /// <param name="numberFormat">The numeric format string, or empty.</param>
        /// <returns>The display string.</returns>
        public static string FormatForDisplay(string raw, string numberFormat)
        {
            if (!TryParseRaw(raw, out var value))
                return raw;
            return string.IsNullOrEmpty(numberFormat)
                ? FormatForEditing(raw)
                : FieldValueFormatter.Format(value, string.Empty, numberFormat);
        }

        /// <summary>
        /// Formats a bound value for editing: the full-precision value in the user's culture, without
        /// group separators, so what the field shows while focused parses back to the same number.
        /// </summary>
        /// <param name="raw">The bound value, in the invariant culture.</param>
        /// <returns>The editing string; a value that does not parse is returned as is.</returns>
        public static string FormatForEditing(string raw)
            => TryParseRaw(raw, out var value) ? value.ToString(CultureInfo.CurrentCulture) : raw;

        /// <summary>
        /// Parses a bound value, the invariant form the wire carries.
        /// </summary>
        /// <param name="raw">The bound value.</param>
        /// <param name="value">The parsed value.</param>
        /// <returns><c>true</c> when the value is a number.</returns>
        public static bool TryParseRaw(string? raw, out decimal value)
            => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// Parses what the user typed: the user's culture, sign and decimal separator only.
        /// </summary>
        /// <param name="text">The typed text.</param>
        /// <param name="value">The parsed value.</param>
        /// <returns><c>true</c> when the text is a number in the accepted shape.</returns>
        public static bool TryParseInput(string? text, out decimal value)
            => decimal.TryParse(text, InputStyles, CultureInfo.CurrentCulture, out value);

        /// <summary>
        /// Resolves the value written back on commit: the parsed input at full precision, or
        /// <paramref name="lastRaw"/> when the input does not parse, so a stray keystroke never corrupts
        /// the bound field.
        /// </summary>
        /// <param name="text">The typed text.</param>
        /// <param name="lastRaw">The last valid bound value.</param>
        /// <returns>The value to write back, in the invariant culture.</returns>
        public static string ToWriteBackValue(string? text, string lastRaw)
            => TryParseInput(text, out var value) ? value.ToString(CultureInfo.InvariantCulture) : lastRaw;

        // Per-row reference code: the field's reference field (currency key / unit) value on the row,
        // then the supplied fallback.
        private static string ResolveReferenceCode(DataRow? row, string referenceField, string fallback)
        {
            if (row is not null
                && StringUtilities.IsNotEmpty(referenceField)
                && row.Table.Columns.Contains(referenceField))
            {
                string code = ValueUtilities.CStr(row[referenceField]);
                if (StringUtilities.IsNotEmpty(code)) { return code; }
            }
            return fallback;
        }
    }
}
