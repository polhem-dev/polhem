using System.Globalization;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Formats a raw field value into its display string, so a value renders identically in a grid cell
    /// and in a form field, and in every UI head. Formatting uses <see cref="CultureInfo.CurrentCulture"/>,
    /// the signed-in user's culture, for separators and date patterns; an explicit display format wins
    /// over the numeric format.
    /// </summary>
    /// <remarks>
    /// Display only. The values themselves travel in the invariant culture, so the display culture
    /// never reaches the wire or a stored value.
    /// </remarks>
    public static class FieldValueFormatter
    {
        /// <summary>
        /// Formats <paramref name="raw"/> using <paramref name="displayFormat"/> when set, otherwise
        /// <paramref name="numberFormat"/>, falling back to a canonical string for dates and other
        /// formattable values. Returns an empty string for <c>null</c> / <see cref="DBNull"/>.
        /// </summary>
        /// <param name="raw">The raw field value.</param>
        /// <param name="displayFormat">An explicit display format string, or empty.</param>
        /// <param name="numberFormat">The numeric format string (e.g. <c>"N2"</c>), or empty.</param>
        /// <returns>The display string.</returns>
        public static string Format(object? raw, string displayFormat, string numberFormat)
        {
            if (raw is null || raw == DBNull.Value) return string.Empty;

            var culture = CultureInfo.CurrentCulture;
            if (!string.IsNullOrEmpty(displayFormat) && raw is IFormattable formattableDisplay)
                return formattableDisplay.ToString(displayFormat, culture);
            if (!string.IsNullOrEmpty(numberFormat) && raw is IFormattable formattableNumber)
                return formattableNumber.ToString(numberFormat, culture);

            return raw switch
            {
                // The culture's short date, and its short date with the long time when the value
                // has a time of day.
                DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                    ? dt.ToString("d", culture)
                    : dt.ToString("G", culture),
                decimal d => WithoutTrailingZeros(d).ToString(culture),
                IFormattable f => f.ToString(null, culture),
                _ => raw.ToString() ?? string.Empty,
            };
        }

        /// <summary>
        /// Returns <paramref name="value"/> with the trailing zeros of its scale removed, so an unformatted value
        /// shows the same whether it came from the database, which reads a decimal with the column's scale
        /// (<c>0.0000</c>), or was typed or defaulted (<c>0</c>).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The same number with the smallest scale that keeps it exact.</returns>
        internal static decimal WithoutTrailingZeros(decimal value)
            // Dividing by one written with the largest scale makes the result's scale the smallest exact one.
            => value / 1.0000000000000000000000000000m;
    }
}
