namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// Reads the form workbook template embedded in this assembly.
/// </summary>
internal static class FormWorkbookTemplate
{
    /// <summary>The manifest resource name; set by <c>LogicalName</c> in the project file.</summary>
    public const string ResourceName = "Polhem.Definition.Spreadsheet.FormSchema.xlsx";

    private static readonly Lazy<byte[]> s_bytes = new(Load);

    public static byte[] GetBytes() => s_bytes.Value;

    private static byte[] Load()
    {
        using var stream = typeof(FormWorkbookTemplate).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
