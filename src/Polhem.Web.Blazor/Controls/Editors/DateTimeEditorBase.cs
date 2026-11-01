using System.Globalization;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// Base component of a date-and-time editor. A UI package commits through
    /// <see cref="TextEditorBase.CommitText"/> when its input exchanges the invariant <c>yyyy-MM-ddTHH:mm:ss</c> form, or
    /// through <see cref="CommitValue"/> when its picker exchanges a <see cref="DateTime"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is the one the client holds, already in the user's time zone: the connector is the only
    /// place that converts time zones (ADR-032), so this editor converts nothing. The rules every head shares (parsing
    /// the binding string, the display format, the binding format written back) are
    /// <see cref="DateTimeFieldFormat"/>.
    /// </para>
    /// <para>
    /// Only a changed value is written back, so leaving the input as shown keeps the stored value
    /// exactly, including fractions of a second the input drops. Clearing it unsets the field.
    /// </para>
    /// </remarks>
    public abstract class DateTimeEditorBase : TextEditorBase
    {
        private const string InputFormat = "yyyy-MM-ddTHH:mm:ss";

        // The input form of the committed value. A commit happens only when Text differs from it.
        private string _shown = string.Empty;

        /// <summary>
        /// Gets the stored value, or <c>null</c> when the field is empty or does not hold a date and time.
        /// </summary>
        protected DateTime? Value { get; private set; }

        /// <summary>
        /// Gets the text the read-only state shows.
        /// </summary>
        protected string ReadOnlyText { get; private set; } = string.Empty;

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
            // The input form needs the time part even at midnight, which the binding string omits.
            Value = DateTimeFieldFormat.TryParseBindingValue(raw, out var value) ? value : null;
            _shown = Value?.ToString(InputFormat, CultureInfo.InvariantCulture) ?? string.Empty;
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

        /// <summary>
        /// Commits the value a picker reported, under the same rules as text: an unchanged value is not written, and
        /// <c>null</c> unsets the field.
        /// </summary>
        /// <param name="value">The picked value.</param>
        protected void CommitValue(DateTime? value)
            => CommitText(value?.ToString(InputFormat, CultureInfo.InvariantCulture));
    }
}
