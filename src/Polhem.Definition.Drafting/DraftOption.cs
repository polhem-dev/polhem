namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// One choice of a <see cref="DraftField"/>, such as one check box of a set on the document.
    /// </summary>
    public sealed class DraftOption
    {
        /// <summary>Gets or sets the stored value, such as <c>annual</c>.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Gets or sets the text shown for the choice. Empty shows the value.</summary>
        public string Text { get; set; } = string.Empty;
    }
}
