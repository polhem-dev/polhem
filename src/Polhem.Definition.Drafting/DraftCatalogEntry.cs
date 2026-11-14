namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// An existing form in the catalog of <see cref="IFormDraftingHost"/>.
    /// </summary>
    public sealed class DraftCatalogEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DraftCatalogEntry"/> class.
        /// </summary>
        /// <param name="progId">The ProgId of the form.</param>
        /// <param name="displayName">The English display name.</param>
        /// <param name="displayNameZhTw">The zh-TW display name; empty when there is none.</param>
        /// <param name="fields">The fields of the form's master table.</param>
        public DraftCatalogEntry(string progId, string displayName, string displayNameZhTw, IReadOnlyList<DraftCatalogField> fields)
        {
            ProgId = progId;
            DisplayName = displayName;
            DisplayNameZhTw = displayNameZhTw;
            Fields = fields;
        }

        /// <summary>Gets the ProgId of the form.</summary>
        public string ProgId { get; }

        /// <summary>Gets the English display name.</summary>
        public string DisplayName { get; }

        /// <summary>Gets the zh-TW display name; empty when there is none.</summary>
        public string DisplayNameZhTw { get; }

        /// <summary>Gets the fields of the form's master table.</summary>
        public IReadOnlyList<DraftCatalogField> Fields { get; }
    }
}
