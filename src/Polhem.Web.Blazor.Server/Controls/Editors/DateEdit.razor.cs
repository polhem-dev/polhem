using System.Globalization;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.DateEdit"/>: the browser's date input bound to a date
    /// field. The read-only state shows the date in the circuit's culture instead of a disabled picker,
    /// as the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.DateEdit</c> swaps its picker for a flat label.
    /// </summary>
    public partial class DateEdit : FieldEditorBase
    {
        /// <summary>
        /// Gets the <c>type</c> of the rendered input.
        /// </summary>
        protected virtual string InputType => "date";

        /// <summary>
        /// Gets the invariant format the date is written back with, which is also the value format the
        /// input exchanges.
        /// </summary>
        protected virtual string ValueFormat => "yyyy-MM-dd";

        /// <summary>
        /// Gets the standard format the read-only state renders the date with in the user's culture.
        /// Defaults to the short date, <c>d</c>.
        /// </summary>
        protected virtual string DisplayFormat => "d";

        /// <summary>
        /// Gets the modifier class that identifies the editor kind in the rendered markup.
        /// </summary>
        protected virtual string ModifierClass => "polhem-field-editor--date";

        /// <summary>
        /// Gets the value shown in the input, in <see cref="ValueFormat"/>.
        /// </summary>
        protected string InputValue { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the text the read-only state shows.
        /// </summary>
        protected string ReadOnlyText { get; private set; } = string.Empty;

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            // A value that carries a time of day is cut to its date part: the input accepts only the
            // date form and would render a blank box for anything else.
            if (DateTimeFieldFormat.TryParseBindingValue(GetValue(), out var date))
            {
                InputValue = date.ToString(ValueFormat, CultureInfo.InvariantCulture);
                ReadOnlyText = date.ToString(DisplayFormat, CultureInfo.CurrentCulture);
            }
            else
            {
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
    }
}
