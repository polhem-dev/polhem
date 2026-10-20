namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Where a form appears in <c>MenuSettings.xml</c>, as described on the Form sheet of a workbook.
/// </summary>
public sealed class MenuPlacement
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
