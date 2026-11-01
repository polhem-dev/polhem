using Polhem.Core;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// Base component of a time-of-day editor: a text input that normalises what the user types to <c>"HH:mm"</c> when
    /// the value is committed, so <c>"8:30"</c> is stored as <c>"08:30"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Text rather than a native time input or a clock picker: display format is meant to equal storage format in every
    /// UI head (ADR-033), and a native input renders per browser locale.
    /// </para>
    /// <para>
    /// Input that does not parse as a time keeps the last committed value rather than clearing the
    /// field, so a stray keystroke never destroys data. Clearing the box is still an explicit way to unset the field.
    /// </para>
    /// </remarks>
    public abstract class TimeEditorBase : TextEditorBase
    {
        // The committed value in storage form.
        private string _value = string.Empty;

        /// <summary>
        /// Initializes a new instance of <see cref="TimeEditorBase"/>.
        /// </summary>
        protected TimeEditorBase()
        {
            MaxLength = ValueUtilities.TimeOnlyLength;
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            _value = ValueUtilities.CTimeString(GetValue());
            Text = _value;
        }

        /// <inheritdoc />
        protected override string? GetWriteBackValue()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                // An emptied box is an explicit "unset"; the empty string is how an unfilled time of day
                // is stored, because midnight is a legal value and cannot stand in for it.
                _value = string.Empty;
            }
            else
            {
                // Text that does not parse is not passed on either: the time column coercion would throw
                // `FormatException` for it inside the event handler.
                string normalized = ValueUtilities.CTimeString(Text);
                if (StringUtilities.IsNotEmpty(normalized)) { _value = normalized; }
            }
            return _value;
        }
    }
}
