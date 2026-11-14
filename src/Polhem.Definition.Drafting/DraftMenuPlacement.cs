namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// Where a drafted form appears in <c>MenuSettings.xml</c>. The host copies it to the menu placement of its import.
    /// </summary>
    public sealed class DraftMenuPlacement
    {
        /// <summary>Gets or sets the id of the menu folder that holds the entry. Empty puts it at the top level.</summary>
        public string FolderId { get; set; } = string.Empty;

        /// <summary>Gets or sets the caption of the folder, used only when the folder does not exist yet.</summary>
        public string FolderCaption { get; set; } = string.Empty;

        /// <summary>Gets or sets the zh-TW caption of the folder.</summary>
        public string FolderCaptionZhTw { get; set; } = string.Empty;

        /// <summary>Gets or sets the caption of the menu entry. Empty falls back to the form's display name.</summary>
        public string EntryCaption { get; set; } = string.Empty;

        /// <summary>Gets or sets the zh-TW caption of the menu entry.</summary>
        public string EntryCaptionZhTw { get; set; } = string.Empty;
    }
}
