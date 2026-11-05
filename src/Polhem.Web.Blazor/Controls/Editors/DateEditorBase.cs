using System.Globalization;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// Base component of a date editor. A UI package commits through <see cref="CommitInput"/> when its input exchanges
    /// text, or through <see cref="CommitDate"/> when its picker exchanges a date.
    /// </summary>
    public abstract class DateEditorBase : FieldEditorBase
    {
        /// <summary>
        /// Gets the invariant format the date is written back with, which is also the value format of
        /// <see cref="InputValue"/>.
        /// </summary>
        protected virtual string ValueFormat => "yyyy-MM-dd";

        /// <summary>
        /// Gets the standard format the date is shown with in the user's culture, in the read-only state and in the
        /// editable one. Defaults to the short date, <c>d</c>.
        /// </summary>
        protected virtual string DisplayFormat => "d";

        /// <summary>
        /// Gets the custom pattern <see cref="DisplayFormat"/> stands for in the user's culture, for a picker that takes a
        /// pattern rather than a standard format. A custom <see cref="DisplayFormat"/> is returned as it is.
        /// </summary>
        protected string DisplayPattern => DisplayFormat.Length == 1
            ? CultureInfo.CurrentCulture.DateTimeFormat.GetAllDateTimePatterns(DisplayFormat[0])[0]
            : DisplayFormat;

        /// <summary>
        /// Gets the stored date, or <c>null</c> when the field is empty or does not hold a date.
        /// </summary>
        protected DateTime? Date { get; private set; }

        /// <summary>
        /// Gets the stored date in <see cref="ValueFormat"/>.
        /// </summary>
        protected string InputValue { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the text the read-only state shows.
        /// </summary>
        protected string ReadOnlyText { get; private set; } = string.Empty;

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            // A value that carries a time of day is cut to its date part: a date input accepts only the
            // date form and would render a blank box for anything else.
            if (DateTimeFieldFormat.TryParseBindingValue(GetValue(), out var date))
            {
                Date = date;
                InputValue = date.ToString(ValueFormat, CultureInfo.InvariantCulture);
                ReadOnlyText = date.ToString(DisplayFormat, CultureInfo.CurrentCulture);
            }
            else
            {
                Date = null;
                InputValue = string.Empty;
                ReadOnlyText = string.Empty;
            }
        }

        /// <summary>
        /// Commits the value the input reported: an empty value unsets the field, a date is written in
        /// <see cref="ValueFormat"/>, and anything else keeps the stored value.
        /// </summary>
        /// <param name="input">The input's value.</param>
        protected void CommitInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                WriteBack(null);
            else if (DateTimeFieldFormat.TryParseBindingValue(input, out var date))
                WriteBack(date.ToString(ValueFormat, CultureInfo.InvariantCulture));
            RefreshFromSource();
        }

        /// <summary>
        /// Commits the date a picker reported: <c>null</c> unsets the field.
        /// </summary>
        /// <remarks>
        /// A picker that reports <c>null</c> for invalid text as well as for a cleared box must tell the two apart
        /// before calling this, because a stray entry must not destroy the stored value.
        /// </remarks>
        /// <param name="date">The picked date.</param>
        protected void CommitDate(DateTime? date)
        {
            WriteBack(date?.ToString(ValueFormat, CultureInfo.InvariantCulture));
            RefreshFromSource();
        }
    }
}
