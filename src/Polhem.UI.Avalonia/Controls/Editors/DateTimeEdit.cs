using System.Globalization;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

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
        // The text shown for the committed value. A commit happens only when Text differs from it.
        private string _display = string.Empty;

        /// <summary>
        /// Initializes a new instance of <see cref="DateTimeEdit"/>.
        /// </summary>
        public DateTimeEdit()
        {
            PlaceholderText = DateTimeFieldFormat.GetInputPattern(CultureInfo.CurrentCulture);
            // Subscribed after the base TextEdit commit handler (registered in its constructor), so
            // this runs once the value is written and replaces the typed text with its formatted form.
            LostFocus += (_, _) => Text = _display;
        }

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
                return string.IsNullOrWhiteSpace(text) || DateTimeFieldFormat.TryParseInput(text, out _);
            }
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            _display = DateTimeFieldFormat.FormatBindingValue(Binder.GetValue());
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
            _ = DateTimeFieldFormat.TryParseInput(Text, out var value);
            _display = DateTimeFieldFormat.FormatDisplay(value);
            return DateTimeFieldFormat.ToBindingValue(value);
        }
    }
}
