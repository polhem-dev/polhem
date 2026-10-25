using System.Globalization;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.DateTimeEdit"/>: an instant shown as a date with its time
    /// of day. Editing uses the browser's date-and-time input; the read-only state shows the value in the
    /// circuit's culture, the way the grid shows it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is the one the client holds, already in the user's time zone: the connector is the only
    /// place that converts time zones (ADR-032), so this editor converts nothing.
    /// </para>
    /// <para>
    /// The Avalonia head uses a culture-formatted text input instead, because a popup picker breaks inside
    /// its grid. A browser's <c>datetime-local</c> input has no such problem and exchanges the invariant
    /// <c>yyyy-MM-ddTHH:mm:ss</c> form in both directions. The rules both heads share (parsing the
    /// binding string, the display format, the binding format written back) are
    /// <see cref="DateTimeFieldFormat"/>.
    /// </para>
    /// <para>
    /// Only a changed value is written back, so leaving the input as shown keeps the stored value
    /// exactly, including fractions of a second the input drops. Clearing it unsets the field.
    /// </para>
    /// </remarks>
    public sealed partial class DateTimeEdit : TextEdit
    {
        // The input form of the committed value. A commit happens only when Text differs from it.
        private string _shown = string.Empty;

        /// <summary>
        /// Gets the text the read-only state shows.
        /// </summary>
        private string ReadOnlyText { get; set; } = string.Empty;

        /// <inheritdoc />
        protected override string ModifierClass => "polhem-field-editor--datetime";

        /// <inheritdoc />
        protected override bool ShouldWriteBackText
        {
            get
            {
                if (string.Equals(Text, _shown, StringComparison.Ordinal)) return false;
                return string.IsNullOrWhiteSpace(Text) || DateTimeFieldFormat.TryParseBindingValue(Text, out _);
            }
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            var raw = GetValue();
            // A datetime-local input needs the time part even at midnight, which the binding string omits.
            _shown = DateTimeFieldFormat.TryParseBindingValue(raw, out var value)
                ? value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                : string.Empty;
            Text = _shown;
            ReadOnlyText = DateTimeFieldFormat.FormatBindingValue(raw);
        }

        /// <inheritdoc />
        protected override string? GetWriteBackValue()
        {
            if (string.IsNullOrWhiteSpace(Text))
                return string.Empty;

            // ShouldWriteBackText admits only text that parses, so this always succeeds.
            _ = DateTimeFieldFormat.TryParseBindingValue(Text, out var value);
            return DateTimeFieldFormat.ToBindingValue(value);
        }
    }
}
