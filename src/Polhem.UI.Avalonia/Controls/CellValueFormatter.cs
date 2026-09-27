using System.Globalization;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// Formats a raw field value into its display string, shared by <see cref="GridControl"/> cells and
    /// the <see cref="Polhem.UI.Avalonia.Controls.Editors.NumericEdit"/> editor so a value renders identically in a grid and in a form field.
    /// Formatting uses <see cref="CultureInfo.CurrentCulture"/>, the signed-in user's culture, for
    /// separators and date patterns; an explicit <c>DisplayFormat</c> wins over the numeric format.
    /// </summary>
    /// <remarks>
    /// Display only. The values themselves travel in the invariant culture, so the display culture
    /// never reaches the wire or a stored value.
    /// </remarks>
    internal static class CellValueFormatter
    {
        /// <summary>
        /// Formats <paramref name="raw"/> using <paramref name="displayFormat"/> when set, otherwise
        /// <paramref name="numberFormat"/>, falling back to a canonical string for dates and other
        /// formattable values. Returns an empty string for <c>null</c> / <see cref="DBNull"/>.
        /// </summary>
        /// <param name="raw">The raw field value.</param>
        /// <param name="displayFormat">An explicit display format string, or empty.</param>
        /// <param name="numberFormat">The numeric format string (e.g. <c>"N2"</c>), or empty.</param>
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
                IFormattable f => f.ToString(null, culture),
                _ => raw.ToString() ?? string.Empty,
            };
        }
    }
}
