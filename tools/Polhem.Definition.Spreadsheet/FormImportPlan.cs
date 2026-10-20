namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// What an import would do: the issues found and the files it would write. Nothing is written until
/// <see cref="FormImporter.Apply"/> is called, so the plan doubles as the dry-run report.
/// </summary>
/// <param name="Issues">The problems found in the workbook and the form.</param>
/// <param name="Files">The files the import touches; empty when there is any error.</param>
public sealed record FormImportPlan(IReadOnlyList<DefinitionIssue> Issues, IReadOnlyList<PlannedFile> Files)
{
    /// <summary>Gets a value indicating whether any issue is an error, in which case nothing is written.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
}
