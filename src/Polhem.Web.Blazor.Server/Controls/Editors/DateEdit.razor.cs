using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.DateEdit"/>: the browser's date input bound to a date
    /// field. The read-only state shows the date in the circuit's culture instead of a disabled picker,
    /// as the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.DateEdit</c> swaps its picker for a flat label.
    /// </summary>
    public partial class DateEdit : DateEditorBase
    {
        /// <summary>
        /// Gets the <c>type</c> of the rendered input.
        /// </summary>
        protected virtual string InputType => "date";

        /// <summary>
        /// Gets the modifier class that identifies the editor kind in the rendered markup.
        /// </summary>
        protected virtual string ModifierClass => "polhem-field-editor--date";
    }
}
