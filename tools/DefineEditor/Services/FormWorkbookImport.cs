using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The steps of a form workbook import that need no window: reading the workbook, planning the files, and the text
/// that reports them. The single import and the batch import share them.
/// </summary>
public static class FormWorkbookImport
{
    /// <summary>Reads <paramref name="workbookPath"/> and, when it has no errors, plans its import with <paramref name="options"/>.</summary>
    /// <exception cref="IOException">The workbook cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not a workbook.</exception>
    public static FormWorkbookImportPreparation Prepare(string workbookPath, FormImportOptions options)
    {
        FormWorkbookReadResult read;
        using (var stream = File.OpenRead(workbookPath))
            read = FormWorkbookReader.Read(stream);

        var plan = read.HasErrors ? null : FormImporter.Plan(read.Content!, options);
        return new FormWorkbookImportPreparation(read, plan);
    }

    /// <summary>The table names of the TableSchema files <paramref name="plan"/> lists, for upgrading only those tables.</summary>
    public static IReadOnlyList<string> TableNames(FormImportPlan plan)
        => plan.Files
            .Where(f => IsTableSchema(f.Path))
            .Select(f => Path.GetFileName(f.Path)[..^".TableSchema.xml".Length])
            .ToList();

    /// <summary>One bullet line per issue: its location and its message.</summary>
    public static string FormatIssues(IEnumerable<DefinitionIssue> issues)
        => string.Join("\n", issues.Select(i => $"• {i.Location}: {i.Message}"));

    /// <summary>What happens to a planned file, in the interface language.</summary>
    public static string ActionLabel(PlannedFileAction action) => LocalizationService.Current[action switch
    {
        PlannedFileAction.Create => "ImportAction_Create",
        PlannedFileAction.Overwrite => "ImportAction_Overwrite",
        PlannedFileAction.Update => "ImportAction_Update",
        _ => "ImportAction_Skip",
    }];

    /// <summary>True for a TableSchema file.</summary>
    public static bool IsTableSchema(string path)
        => path.EndsWith(".TableSchema.xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a FormLayout file.</summary>
    public static bool IsFormLayout(string path)
        => path.EndsWith(".FormLayout.xml", StringComparison.OrdinalIgnoreCase);
}
