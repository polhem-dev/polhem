namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// A text shown in the workbook, in every <see cref="FormWorkbookLanguage"/>.
/// </summary>
/// <param name="En">The English text.</param>
/// <param name="ZhTw">The Traditional Chinese text.</param>
internal readonly record struct SheetText(string En, string ZhTw)
{
    /// <summary>A text that is empty in every language.</summary>
    public static SheetText Empty { get; } = new(string.Empty, string.Empty);

    public string Get(FormWorkbookLanguage language)
        => language == FormWorkbookLanguage.TraditionalChinese ? ZhTw : En;
}
