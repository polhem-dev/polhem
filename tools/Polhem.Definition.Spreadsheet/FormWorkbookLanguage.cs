namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// The language of the headers, hints and instructions of a form workbook.
/// </summary>
/// <remarks>
/// Only the text shown to the person filling in the workbook changes with the language. Both languages carry the same
/// sheets, the same hidden key row and the same English and zh-TW text columns, so <see cref="FormWorkbookReader"/>
/// reads a workbook of either language.
/// </remarks>
public enum FormWorkbookLanguage
{
    /// <summary>English headers, hints and an <c>Instructions</c> sheet.</summary>
    English,

    /// <summary>Traditional Chinese headers, hints and a <c>說明</c> sheet.</summary>
    TraditionalChinese,
}
