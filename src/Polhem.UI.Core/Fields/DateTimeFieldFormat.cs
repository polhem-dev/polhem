using System.Globalization;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Display, input and binding-string rules for a date-and-time field (an instant), shared by the
    /// date-time editors and grid cells of every UI head.
    /// </summary>
    /// <remarks>
    /// The value handled here is the one the client holds, already in the user's time zone: the
    /// connector is the only place that converts time zones (ADR-032), so nothing here converts.
    /// </remarks>
    public static class DateTimeFieldFormat
    {
        // The binding string format written back: the invariant round-trip shape the data objects
        // parse, with the time kept even at midnight.
        private const string BindingFormat = "yyyy-MM-ddTHH:mm:ss";

        /// <summary>
        /// Formats <paramref name="value"/> the way editors and grid cells display an instant: the user's
        /// culture short date with its long time (<c>G</c>), also at midnight.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>The display string.</returns>
        public static string FormatDisplay(DateTime value)
            => value.ToString("G", CultureInfo.CurrentCulture);

        /// <summary>
        /// Formats a binding string (the invariant form a data object hands to editors) for display;
        /// an empty or unparseable string gives an empty display.
        /// </summary>
        /// <param name="bindingValue">The binding string.</param>
        /// <returns>The display string, or empty.</returns>
        public static string FormatBindingValue(string? bindingValue)
            => TryParseBindingValue(bindingValue, out var value) ? FormatDisplay(value) : string.Empty;

        /// <summary>
        /// Parses user input: the user's culture first, then the invariant culture, so an ISO
        /// <c>yyyy-MM-dd HH:mm</c> is always accepted.
        /// </summary>
        /// <param name="input">The typed text.</param>
        /// <param name="value">The parsed value, with an unspecified kind.</param>
        /// <returns><c>true</c> when the text is a date, with or without a time of day.</returns>
        public static bool TryParseInput(string? input, out DateTime value)
        {
            const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces;
            if (string.IsNullOrWhiteSpace(input))
            {
                value = default;
                return false;
            }
            if (DateTime.TryParse(input, CultureInfo.CurrentCulture, styles, out value)
                || DateTime.TryParse(input, CultureInfo.InvariantCulture, styles, out value))
            {
                value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parses a binding string, the invariant form a data object hands to editors.
        /// </summary>
        /// <param name="bindingValue">The binding string.</param>
        /// <param name="value">The parsed value.</param>
        /// <returns><c>true</c> when the string is a date, with or without a time of day.</returns>
        public static bool TryParseBindingValue(string? bindingValue, out DateTime value)
        {
            if (!string.IsNullOrEmpty(bindingValue)
                && DateTime.TryParse(bindingValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            {
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Renders <paramref name="value"/> as the binding string written back to the data object.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>The invariant binding string, with the time of day even at midnight.</returns>
        public static string ToBindingValue(DateTime value)
            => value.ToString(BindingFormat, CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets the input pattern shown as a placeholder: the culture's short date and long time patterns.
        /// </summary>
        /// <param name="culture">The culture whose patterns to use.</param>
        /// <returns>The pattern text.</returns>
        public static string GetInputPattern(CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull(culture);
            return culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.LongTimePattern;
        }
    }
}
