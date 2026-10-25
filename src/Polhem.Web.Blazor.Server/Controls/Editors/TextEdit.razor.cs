using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.TextEdit"/>: a text input two-way bound to a
    /// <see cref="Polhem.Web.Blazor.Server.DataObjects.FormDataObject"/> field, applying
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
    public partial class TextEdit : FieldEditorBase
    {
        /// <summary>
        /// Gets or sets the text shown in the input.
        /// </summary>
        protected string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum number of characters the input accepts; zero for no limit.
        /// </summary>
        protected int MaxLength { get; set; }

        /// <summary>
        /// Gets the modifier class that identifies the editor kind in the rendered markup.
        /// </summary>
        protected virtual string ModifierClass => "polhem-field-editor--text";

        /// <summary>
        /// Gets the <c>inputmode</c> hint for on-screen keyboards, or <c>null</c> for none.
        /// </summary>
        protected virtual string? InputMode => null;

        /// <summary>
        /// Gets the <c>pattern</c> attribute, or <c>null</c> for none.
        /// </summary>
        protected virtual string? Pattern => null;

        /// <summary>
        /// Gets the placeholder text, or <c>null</c> for none.
        /// </summary>
        protected virtual string? Placeholder => null;

        /// <summary>
        /// Whether a committed text writes back to the bound field. Editors that reject some input
        /// override this so the stored value is kept.
        /// </summary>
        protected virtual bool ShouldWriteBackText => true;

        /// <summary>
        /// Gets the <c>maxlength</c> attribute value, or <c>null</c> to leave the attribute out.
        /// </summary>
        protected int? MaxLengthAttribute => MaxLength > 0 ? MaxLength : null;

        /// <summary>
        /// Resolves the value written back to the bound field on commit. The base editor writes the raw
        /// <see cref="Text"/>; derived editors parse it and write the underlying value.
        /// </summary>
        /// <returns>The binding string to write.</returns>
        protected virtual string? GetWriteBackValue() => Text;

        /// <summary>
        /// Commits <paramref name="text"/> the user entered: writes it back when
        /// <see cref="ShouldWriteBackText"/> allows, then shows the stored value again.
        /// </summary>
        /// <param name="text">The text the input reported.</param>
        protected void CommitText(string? text)
        {
            Text = text ?? string.Empty;
            if (ShouldWriteBackText)
                WriteBack(GetWriteBackValue());
            RefreshFromSource();
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            Text = GetValue();
        }

        /// <inheritdoc />
        protected override void ApplyMetadata()
        {
            if (FormField is { MaxLength: > 0 } formField)
                MaxLength = formField.MaxLength;
        }
    }
}
