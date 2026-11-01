using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.TextEdit"/>: a text input two-way bound to a
    /// <see cref="Polhem.Web.Blazor.DataObjects.FormDataObject"/> field, applying
    /// <see cref="Polhem.Definition.Forms.FormField.MaxLength"/> and the form mode automatically.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is committed when the user leaves the input or presses Enter (the browser's
    /// <c>change</c> event), not per keystroke, so any recalculation that depends on it runs once the
    /// user finishes the value. This matches the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.TextEdit</c>.
    /// </para>
    /// <para>
    /// The input is bound with <c>@bind:get</c> / <c>@bind:set</c> rather than a <c>value</c> attribute and
    /// an <c>onchange</c> handler. When a derived editor rejects the input and keeps the stored value,
    /// the re-render emits the same <c>value</c> as before; only the bind form makes the renderer push
    /// that kept value back into the box instead of leaving the typo on screen.
    /// </para>
    /// </remarks>
    public partial class TextEdit : TextEditorBase
    {
        /// <summary>
        /// Gets the modifier class that identifies the editor kind in the rendered markup.
        /// </summary>
        protected virtual string ModifierClass => "polhem-field-editor--text";

        /// <summary>
        /// Gets the <c>maxlength</c> attribute value, or <c>null</c> to leave the attribute out.
        /// </summary>
        protected int? MaxLengthAttribute => MaxLength > 0 ? MaxLength : null;
    }
}
