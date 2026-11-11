namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// Base component of the editors that exchange text with the user: holds the text on screen and commits it to the
    /// bound field. A UI package renders the input and calls <see cref="CommitText"/> when the user finishes the value.
    /// </summary>
    /// <remarks>
    /// Commit when the user leaves the input or presses Enter, not per keystroke, so any recalculation that depends on
    /// the value runs once.
    /// </remarks>
    public abstract class TextEditorBase : FieldEditorBase
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
        /// Whether a committed text writes back to the bound field. Editors that reject some input
        /// override this so the stored value is kept.
        /// </summary>
        protected virtual bool ShouldWriteBackText => true;

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

        /// <summary>
        /// Commits <paramref name="text"/> like <see cref="CommitText"/>, and returns whether the editor took it.
        /// </summary>
        /// <param name="text">The text the input reported.</param>
        /// <returns>
        /// <c>false</c> when the entry was rejected: the shown text stayed as it was although it differs from what was
        /// typed. An input whose value does not change keeps showing what was typed, so a UI package recreates it then.
        /// </returns>
        protected bool TryCommitText(string? text)
        {
            var shown = Text;
            CommitText(text);
            return !string.Equals(Text, shown, StringComparison.Ordinal) || string.Equals(Text, text, StringComparison.Ordinal);
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
