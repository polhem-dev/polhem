namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// The outcome of <see cref="FormWorkbookReader.Read"/>.
/// </summary>
/// <param name="Content">The content read, or <see langword="null"/> when the workbook could not be read at all.</param>
/// <param name="Issues">Problems found in the workbook, each located at a cell or row.</param>
public sealed record FormWorkbookReadResult(FormWorkbookContent? Content, IReadOnlyList<DefinitionIssue> Issues)
{
    /// <summary>Gets a value indicating whether any issue is an error.</summary>
    public bool HasErrors => Content is null || Issues.Any(i => i.Severity == IssueSeverity.Error);
}
