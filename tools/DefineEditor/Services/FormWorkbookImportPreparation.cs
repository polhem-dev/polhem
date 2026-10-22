using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.Services;

/// <summary>A form workbook read and, when it had no errors, planned for import.</summary>
/// <param name="Read">What the workbook held.</param>
/// <param name="Plan">The planned files, or <see langword="null"/> when the workbook has errors.</param>
public sealed record FormWorkbookImportPreparation(FormWorkbookReadResult Read, FormImportPlan? Plan)
{
    /// <summary>The issues of reading and of planning together.</summary>
    public IReadOnlyList<DefinitionIssue> Issues { get; } = [.. Read.Issues, .. Plan?.Issues ?? []];

    /// <summary>True when the workbook cannot be imported.</summary>
    public bool HasErrors => Plan is null || Plan.HasErrors;
}
