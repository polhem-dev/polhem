using System.Globalization;
using Polhem.Definition.Layouts;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.DateTimeEdit"/>: a text input that shows an instant as a
    /// date with its time of day in the user's culture and parses what the user types in the same
    /// culture, so the time part survives an edit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value shown is the one the client holds, already in the user's time zone: the connector is
    /// the only place that converts time zones (ADR-032), so this editor converts nothing.
    /// </para>
    /// <para>
    /// It is a text input rather than a date picker next to a time picker. The same input works as a
    /// grid cell editor, where a popup picker is torn down by the grid's edit pipeline (ADR-021), and it
    /// stays one line wide in a phone-width form, where two side-by-side pickers would crowd.
    /// </para>
    /// <para>
    /// Only a changed text is written back. Leaving the text as shown keeps the stored value exactly,
    /// including the fractions of a second the display drops. Input that does not parse keeps the last
    /// committed value, matching <see cref="NumericEdit"/> and <see cref="TimeEdit"/>; clearing the box
    /// unsets the field.
    /// </para>
    /// </remarks>
    public sealed class DateTimeEdit : TextEdit
    {
        // The binding string format written back: the invariant round-trip shape the data objects
        // parse, with the time kept even at midnight.
        private const string BindingFormat = "yyyy-MM-ddTHH:mm:ss";

        // The text shown for the committed value. A commit happens only when Text differs from it.
        private string _display = string.Empty;

        /// <summary>
        /// Initializes a new instance of <see cref="DateTimeEdit"/>.
        /// </summary>
        public DateTimeEdit()
        {
            PlaceholderText = GetInputPattern(CultureInfo.CurrentCulture);
            // Subscribed after the base TextEdit commit handler (registered in its constructor), so
            // this runs once the value is written and replaces the typed text with its formatted form.
            LostFocus += (_, _) => Text = _display;
        }

        /// <summary>
        /// Formats <paramref name="value"/> the way the editor and the grid cells display an instant:
        /// the user's culture short date with its long time (<c>G</c>), also at midnight.
        /// </summary>
        /// <param name="value">The value to format.</param>
        internal static string FormatDisplay(DateTime value)
            => value.ToString("G", CultureInfo.CurrentCulture);

        /// <summary>
        /// Formats a binding string (the invariant form a data object hands to editors) for display;
        /// an empty or unparseable string gives an empty display.
        /// </summary>
        /// <param name="bindingValue">The binding string.</param>
        internal static string FormatBindingValue(string? bindingValue)
            => TryParseBindingValue(bindingValue, out var value) ? FormatDisplay(value) : string.Empty;

        /// <summary>
        /// Parses user input: the user's culture first, then the invariant culture, so an ISO
        /// <c>yyyy-MM-dd HH:mm</c> is always accepted.
        /// </summary>
        /// <param name="input">The typed text.</param>
        /// <param name="value">The parsed value, with an unspecified kind.</param>
        /// <returns><c>true</c> when the text is a date, with or without a time of day.</returns>
        internal static bool TryParseInput(string? input, out DateTime value)
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
        /// Renders <paramref name="value"/> as the binding string written back to the data object.
        /// </summary>
        /// <param name="value">The value to write.</param>
        internal static string ToBindingValue(DateTime value)
            => value.ToString(BindingFormat, CultureInfo.InvariantCulture);

        /// <inheritdoc />
        /// <remarks>
        /// <c>false</c> for an unchanged text, so an untouched value is not rewritten at the display's
        /// precision, and for text that does not parse, so a typo never replaces the stored value.
        /// </remarks>
        protected override bool ShouldWriteBackText
        {
            get
            {
                string text = Text ?? string.Empty;
                if (string.Equals(text, _display, StringComparison.Ordinal)) return false;
                return string.IsNullOrWhiteSpace(text) || TryParseInput(text, out _);
            }
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            _display = FormatBindingValue(Binder.GetValue());
            Text = _display;
        }

        /// <inheritdoc />
        protected override string? GetWriteBackValue()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                _display = string.Empty;
                return string.Empty;
            }

            // ShouldWriteBackText admits only text that parses, so this always succeeds.
            _ = TryParseInput(Text, out var value);
            _display = FormatDisplay(value);
            return ToBindingValue(value);
        }

        private static bool TryParseBindingValue(string? bindingValue, out DateTime value)
        {
            if (!string.IsNullOrEmpty(bindingValue)
                && DateTime.TryParse(bindingValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            {
                return true;
            }
            value = default;
            return false;
        }

        private static string GetInputPattern(CultureInfo culture)
            => culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.LongTimePattern;
    }
}
