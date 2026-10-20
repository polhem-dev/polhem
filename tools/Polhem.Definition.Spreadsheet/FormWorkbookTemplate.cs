namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Reads the form workbook templates embedded in this assembly, one per <see cref="FormWorkbookLanguage"/>.
/// </summary>
internal static class FormWorkbookTemplate
{
    private static readonly Lazy<byte[]> s_english = new(() => Load(FormWorkbookLanguage.English));
    private static readonly Lazy<byte[]> s_traditionalChinese = new(() => Load(FormWorkbookLanguage.TraditionalChinese));

    /// <summary>
    /// Gets the file name of a template, which is also the last part of its manifest resource name; the
    /// <c>LogicalName</c> of each embedded template in the project file must follow it.
    /// </summary>
    public static string FileName(FormWorkbookLanguage language)
        => $"FormSchema.{FormWorkbookLanguages.ToTag(language)}.xlsx";

    public static string ResourceName(FormWorkbookLanguage language)
        => "Polhem.Definition.Spreadsheet." + FileName(language);

    public static byte[] GetBytes(FormWorkbookLanguage language)
        => language == FormWorkbookLanguage.TraditionalChinese ? s_traditionalChinese.Value : s_english.Value;

    private static byte[] Load(FormWorkbookLanguage language)
    {
        var name = ResourceName(language);
        using var stream = typeof(FormWorkbookTemplate).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded resource '{name}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
