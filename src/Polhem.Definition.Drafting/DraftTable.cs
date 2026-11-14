namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A table of a <see cref="FormDraft"/>: the master table or one detail table.
    /// </summary>
    public sealed class DraftTable
    {
        /// <summary>
        /// Gets or sets the table name, such as <c>LeaveRequestDetail</c>. Ignored for the master table, which is always
        /// named after the form's ProgId.
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>Gets or sets the English display name of the table.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the zh-TW display name of the table.</summary>
        public string DisplayNameZhTw { get; set; } = string.Empty;

        /// <summary>Gets the fields of the table, in the order they appear on the document.</summary>
        public IList<DraftField> Fields { get; } = [];
    }
}
