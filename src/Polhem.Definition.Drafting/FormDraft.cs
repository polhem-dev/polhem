namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A form being drafted from a document: what the model proposed and the user confirmed so far. Every part may still
    /// be missing; <see cref="FormDraftConverter"/> turns whatever is there into a form.
    /// </summary>
    public sealed class FormDraft
    {
        /// <summary>Gets or sets the ProgId of the form, such as <c>LeaveRequest</c>. Empty while undecided.</summary>
        public string ProgId { get; set; } = string.Empty;

        /// <summary>Gets or sets the English display name of the form.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the zh-TW display name of the form.</summary>
        public string DisplayNameZhTw { get; set; } = string.Empty;

        /// <summary>
        /// Gets the tables of the form. The first is the master table, whatever its name; the others are detail tables.
        /// </summary>
        public IList<DraftTable> Tables { get; } = [];

        /// <summary>Gets where the form appears in the menu.</summary>
        public DraftMenuPlacement Menu { get; } = new();
    }
}
