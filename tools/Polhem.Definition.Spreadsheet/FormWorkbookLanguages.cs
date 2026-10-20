using System.Globalization;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Converts between <see cref="FormWorkbookLanguage"/> and language tags or cultures.
/// </summary>
public static class FormWorkbookLanguages
{
    /// <summary>The language tag of <see cref="FormWorkbookLanguage.English"/>.</summary>
    public const string EnglishTag = "en";

    /// <summary>The language tag of <see cref="FormWorkbookLanguage.TraditionalChinese"/>.</summary>
    public const string TraditionalChineseTag = "zh-TW";

    /// <summary>
    /// Parses a language tag, ignoring case.
    /// </summary>
    /// <param name="tag"><c>en</c> or <c>zh-TW</c>.</param>
    /// <param name="language">The language the tag names.</param>
    /// <returns><see langword="true"/> when the tag names a workbook language.</returns>
    public static bool TryParse(string? tag, out FormWorkbookLanguage language)
    {
        if (string.Equals(tag, EnglishTag, StringComparison.OrdinalIgnoreCase))
        {
            language = FormWorkbookLanguage.English;
            return true;
        }
        if (string.Equals(tag, TraditionalChineseTag, StringComparison.OrdinalIgnoreCase))
        {
            language = FormWorkbookLanguage.TraditionalChinese;
            return true;
        }
        language = FormWorkbookLanguage.English;
        return false;
    }

    /// <summary>
    /// Picks the workbook language for a user interface culture: Traditional Chinese for any Chinese culture,
    /// English for every other.
    /// </summary>
    /// <param name="culture">The user interface culture.</param>
    /// <returns>The workbook language.</returns>
    public static FormWorkbookLanguage FromCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return string.Equals(culture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
            ? FormWorkbookLanguage.TraditionalChinese
            : FormWorkbookLanguage.English;
    }

    /// <summary>
    /// Gets the language tag of a workbook language.
    /// </summary>
    /// <param name="language">The workbook language.</param>
    /// <returns><c>en</c> or <c>zh-TW</c>.</returns>
    public static string ToTag(FormWorkbookLanguage language) => language switch
    {
        FormWorkbookLanguage.TraditionalChinese => TraditionalChineseTag,
        _ => EnglishTag,
    };
}
