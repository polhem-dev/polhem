using Polhem.Core;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.TimeEdit"/>: a fixed-width time-of-day input that
    /// normalises what the user types to <c>"HH:mm"</c> when the value is committed, so <c>"8:30"</c> is
    /// stored as <c>"08:30"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A text input rather than <c>type="time"</c>: the value format is the same either way, but a native
    /// time input renders per browser locale, and display format is meant to equal storage format in
    /// every UI head (ADR-033).
    /// </para>
    /// <para>
    /// Input that does not parse as a time keeps the last committed value rather than clearing the
    /// field, matching <see cref="NumericEdit"/>, so a stray keystroke never destroys data. Clearing the
    /// box is still an explicit way to unset the field.
    /// </para>
    /// </remarks>
    public sealed class TimeEdit : TextEdit
    {
        // The committed value in storage form.
        private string _value = string.Empty;

        /// <summary>
        /// Initializes a new instance of <see cref="TimeEdit"/>.
        /// </summary>
        public TimeEdit()
        {
            MaxLength = ValueUtilities.TimeOnlyLength;
        }

        /// <inheritdoc />
        protected override string ModifierClass => "polhem-field-editor--time";

        /// <inheritdoc />
        protected override string? InputMode => "numeric";

        /// <inheritdoc />
        protected override string? Pattern => "[0-2][0-9]:[0-5][0-9]";

        /// <inheritdoc />
        protected override string? Placeholder => "HH:mm";

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
