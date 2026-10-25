namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// The caption convention for field state that every UI head applies to master field captions and
    /// detail grid headers: a required field's caption is highlighted, and a read-only grid header is
    /// parenthesised. Read-only wins when a field is both: a read-only field cannot be filled in.
    /// </summary>
    public static class FieldCaptionRules
    {
        /// <summary>
        /// Marks a read-only field's caption by wrapping it in parentheses (e.g. <c>Amount</c> →
        /// <c>(Amount)</c>); returns the caption unchanged when the field is editable or the caption is empty.
        /// </summary>
        /// <param name="caption">The field caption.</param>
        /// <param name="readOnly">Whether the field is read-only.</param>
        /// <returns>The caption to show.</returns>
        public static string FormatCaption(string caption, bool readOnly)
            => readOnly && !string.IsNullOrEmpty(caption) ? $"({caption})" : caption;

        /// <summary>
        /// Determines whether a caption shows the required cue: the field is required and not read-only.
        /// </summary>
        /// <param name="readOnly">Whether the field is read-only.</param>
        /// <param name="required">Whether the field is required.</param>
        /// <returns><c>true</c> when the caption is highlighted as required.</returns>
        public static bool ShowsRequiredCue(bool readOnly, bool required) => required && !readOnly;
    }
}
