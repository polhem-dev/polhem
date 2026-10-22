using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
using Polhem.Definition;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>
    /// Writes the framework default define files the open solution does not have yet, after the user
    /// confirms the list. Existing files are never touched.
    /// </summary>
    public async Task AddMissingFrameworkDefaultsAsync()
    {
        if (!IsSolutionOpened) return;
        var definePath = SolutionPath;

        var missing = Defaults.ListEmbedded()
            .Where(rel => !File.Exists(Path.Combine(definePath, rel)))
            .ToList();
        if (missing.Count == 0)
        {
            StatusText = L("Status_FrameworkDefaultsComplete");
            return;
        }

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(
                owner,
                L("Confirm_AddFrameworkDefaultsTitle"),
                L("Confirm_AddFrameworkDefaultsMessage", definePath,
                    string.Join("\n", missing.Select(rel => "• " + rel))),
                confirmLabel: L("Action_Add"));
            if (!confirmed)
            {
                StatusText = L("Status_FrameworkDefaultsCancelled");
                return;
            }
        }

        try
        {
            // The filter keeps the write to exactly the list the user confirmed.
            var shown = missing.ToHashSet(StringComparer.Ordinal);
            var result = Defaults.MaterializeTo(definePath, new MaterializeOptions { Filter = shown.Contains });

            RebuildTrees(definePath);
            StatusText = L("Status_FrameworkDefaultsMaterialised", result.WrittenCount);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = L("Status_FrameworkDefaultsFailed", ex.Message);
        }
    }

    /// <summary>
    /// Imports a form workbook into the open solution: reads it, shows the files the import would change
    /// (or the problems that keep it from importing), and writes them once the user confirms.
    /// </summary>
    /// <param name="workbookPath">The <c>.xlsx</c> file to import.</param>
    public async Task ImportFormWorkbookAsync(string workbookPath)
    {
        if (!IsSolutionOpened) return;
        var definePath = SolutionPath;
        var owner = GetOwnerWindow();
        string? imported = null;
        IReadOnlyList<string> importedTables = [];

        try
        {
            var prepared = FormWorkbookImport.Prepare(workbookPath, new FormImportOptions { DefinePath = definePath });
            var read = prepared.Read;
            var plan = prepared.Plan;

            // An existing TableSchema may carry what the form cannot derive (lengths, extra columns, indexes), and an
            // existing FormLayout may have been adjusted by hand, so the user picks how to treat each; the plan above
            // used the defaults, Merge and keep.
            if (plan is { HasErrors: false } && owner is not null)
            {
                var tableSchemaMode = TableSchemaImportMode.Merge;
                if (plan.Files.Any(f => FormWorkbookImport.IsTableSchema(f.Path) && File.Exists(f.Path)))
                {
                    var mode = await ConfirmationDialog.ShowChoiceAsync(owner, L("Confirm_ImportFormTitle"),
                        L("Choice_TableSchemaModeMessage"),
                        [L("Choice_TableSchemaMerge"), L("Choice_TableSchemaKeep"), L("Choice_TableSchemaOverwrite")],
                        selectedIndex: 0, confirmLabel: L("Action_Next"));
                    if (mode is null)
                    {
                        StatusText = L("Status_FormImportCancelled");
                        return;
                    }
                    tableSchemaMode = mode switch
                    {
                        1 => TableSchemaImportMode.Keep,
                        2 => TableSchemaImportMode.Overwrite,
                        _ => TableSchemaImportMode.Merge,
                    };
                }

                var regenerateLayout = false;
                if (plan.Files.Any(f => FormWorkbookImport.IsFormLayout(f.Path) && f.Action == PlannedFileAction.Skip))
                {
                    var layout = await ConfirmationDialog.ShowChoiceAsync(owner, L("Confirm_ImportFormTitle"),
                        L("Choice_FormLayoutMessage"),
                        [L("Choice_FormLayoutKeep"), L("Choice_FormLayoutRegenerate")],
                        selectedIndex: 0, confirmLabel: L("Action_Next"));
                    if (layout is null)
                    {
                        StatusText = L("Status_FormImportCancelled");
                        return;
                    }
                    regenerateLayout = layout == 1;
                }

                if (tableSchemaMode != TableSchemaImportMode.Merge || regenerateLayout)
                {
                    // The conventions already ran on this content and change nothing when applied again.
                    plan = FormImporter.Plan(read.Content!, new FormImportOptions
                    {
                        DefinePath = definePath,
                        TableSchemaMode = tableSchemaMode,
                        RegenerateLayout = regenerateLayout,
                    });
                }
            }
            var issues = read.Issues.Concat(plan?.Issues ?? []).ToList();
            if (plan is null || plan.HasErrors)
            {
                if (owner is not null)
                {
                    await ConfirmationDialog.ShowMessageAsync(owner, L("Error_ImportFormTitle"),
                        L("Error_ImportFormMessage", FormWorkbookImport.FormatIssues(issues)));
                }
                StatusText = L("Status_FormImportCancelled");
                return;
            }

            var progId = read.Content!.Schema.ProgId;
            var written = plan.Files.Where(f => f.Writes).ToList();
            if (written.Count == 0)
            {
                StatusText = L("Status_FormImportUnchanged", progId);
                return;
            }

            // An open editor holds its own copy of the file; replacing the file under unsaved edits would
            // lose one side or the other.
            var dirty = OpenDocuments
                .OfType<ObjectTreeDocumentViewModelBase>()
                .FirstOrDefault(d => d.IsDirty && written.Any(f => SamePath(f.Path, d.FilePath)));
            if (dirty is not null)
            {
                StatusText = L("Status_FormImportBlockedByDirty", Path.GetFileName(dirty.FilePath));
                return;
            }

            if (owner is not null)
            {
                var message = L("Confirm_ImportFormMessage", Path.GetFileName(workbookPath), definePath,
                    string.Join("\n", plan.Files
                        .Where(f => f.Writes || f.Action == PlannedFileAction.Skip)
                        .Select(f => $"• {Path.GetRelativePath(definePath, f.Path)} ({FormWorkbookImport.ActionLabel(f.Action)})")));
                var warnings = issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
                if (warnings.Count > 0)
                    message += "\n\n" + L("Confirm_ImportFormWarnings", FormWorkbookImport.FormatIssues(warnings));
                if (!await ConfirmationDialog.ShowAsync(owner, L("Confirm_ImportFormTitle"), message, confirmLabel: L("Action_Import")))
                {
                    StatusText = L("Status_FormImportCancelled");
                    return;
                }
            }

            FormImporter.Apply(plan);
            CloseDocuments(OpenDocuments
                .OfType<ObjectTreeDocumentViewModelBase>()
                .Where(d => written.Any(f => SamePath(f.Path, d.FilePath)))
                .ToList<DocumentViewModelBase>(), ActiveDocument);
            // The plan lists the FormSchema first.
            RebuildTrees(definePath, revealFilePath: plan.Files[0].Path);
            var schemaNode = FindNodeByFilePath(Nodes[0], plan.Files[0].Path);
            if (schemaNode is not null) SelectedNode = schemaNode;
            StatusText = L("Status_FormImported", progId, written.Count);
            importedTables = FormWorkbookImport.TableNames(plan);
            imported = L("Status_FormImported", progId, written.Count) + "\n" + string.Join("\n",
                written.Select(f => $"• {Path.GetRelativePath(definePath, f.Path)} ({FormWorkbookImport.ActionLabel(f.Action)})"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText = L("Status_FormImportFailed", ex.Message);
        }

        if (imported is not null && owner is not null)
            await OfferDatabaseUpgradeAsync(owner, imported, importedTables);
    }
}
