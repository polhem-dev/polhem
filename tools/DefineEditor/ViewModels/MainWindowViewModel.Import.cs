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
        FormWorkbookImportPreparation prepared;
        try
        {
            prepared = FormWorkbookImport.Prepare(workbookPath, new FormImportOptions { DefinePath = SolutionPath });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText = L("Status_FormImportFailed", ex.Message);
            return;
        }
        await ImportFormContentAsync(prepared.Read.Content, prepared.Read.Issues, prepared.Plan,
            Path.GetFileName(workbookPath), GetOwnerWindow());
    }

    /// <summary>
    /// The part of a form import that does not depend on where the form came from: asks how to treat the existing
    /// TableSchema and FormLayout, shows the files the import would change (or the problems that keep it from importing),
    /// writes them once the user confirms, rebuilds the tree and offers to upgrade the database.
    /// </summary>
    /// <param name="content">The form; <see langword="null"/> when reading it failed.</param>
    /// <param name="readIssues">What reading the form found.</param>
    /// <param name="plan">The plan made with the default modes, or <see langword="null"/> when reading the form failed.</param>
    /// <param name="sourceLabel">What the form was read from, such as the workbook's file name.</param>
    /// <param name="owner">The window the dialogs open over; <see langword="null"/> runs without asking.</param>
    /// <param name="onWritten">
    /// Runs once the files are written, before the database upgrade is offered over the main window, such as to close
    /// the window the import started from.
    /// </param>
    /// <returns>Whether files were written.</returns>
    internal async Task<bool> ImportFormContentAsync(FormWorkbookContent? content, IReadOnlyList<DefinitionIssue> readIssues,
        FormImportPlan? plan, string sourceLabel, Avalonia.Controls.Window? owner, Action? onWritten = null)
    {
        if (!IsSolutionOpened) return false;
        var definePath = SolutionPath;
        string? imported = null;
        IReadOnlyList<string> importedTables = [];

        try
        {
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
                        return false;
                    }
                    tableSchemaMode = mode switch
                    {
                        1 => TableSchemaImportMode.Keep,
                        2 => TableSchemaImportMode.Overwrite,
                        _ => TableSchemaImportMode.Merge,
                    };
                }

                var layoutMode = FormLayoutImportMode.Keep;
                if (plan.Files.Any(f => FormWorkbookImport.IsFormLayout(f.Path) && f.Action == PlannedFileAction.Skip))
                {
                    var layout = await ConfirmationDialog.ShowChoiceAsync(owner, L("Confirm_ImportFormTitle"),
                        L("Choice_FormLayoutMessage"),
                        [L("Choice_FormLayoutKeep"), L("Choice_FormLayoutMerge"), L("Choice_FormLayoutRegenerate")],
                        selectedIndex: 0, confirmLabel: L("Action_Next"));
                    if (layout is null)
                    {
                        StatusText = L("Status_FormImportCancelled");
                        return false;
                    }
                    layoutMode = layout switch
                    {
                        1 => FormLayoutImportMode.Merge,
                        2 => FormLayoutImportMode.Regenerate,
                        _ => FormLayoutImportMode.Keep,
                    };
                }

                if (tableSchemaMode != TableSchemaImportMode.Merge || layoutMode != FormLayoutImportMode.Keep)
                {
                    // The conventions already ran on this content and change nothing when applied again.
                    plan = FormImporter.Plan(content!, new FormImportOptions
                    {
                        DefinePath = definePath,
                        TableSchemaMode = tableSchemaMode,
                        LayoutMode = layoutMode,
                    });
                }
            }
            var issues = readIssues.Concat(plan?.Issues ?? []).ToList();
            if (plan is null || plan.HasErrors)
            {
                if (owner is not null)
                {
                    await ConfirmationDialog.ShowMessageAsync(owner, L("Error_ImportFormTitle"),
                        L("Error_ImportFormMessage", FormWorkbookImport.FormatIssues(issues)));
                }
                StatusText = L("Status_FormImportCancelled");
                return false;
            }

            var progId = content!.Schema.ProgId;
            var written = plan.Files.Where(f => f.Writes).ToList();
            if (written.Count == 0)
            {
                StatusText = L("Status_FormImportUnchanged", progId);
                return false;
            }

            // An open editor holds its own copy of the file; replacing the file under unsaved edits would
            // lose one side or the other.
            var dirty = OpenDocuments
                .OfType<ObjectTreeDocumentViewModelBase>()
                .FirstOrDefault(d => d.IsDirty && written.Any(f => SamePath(f.Path, d.FilePath)));
            if (dirty is not null)
            {
                StatusText = L("Status_FormImportBlockedByDirty", Path.GetFileName(dirty.FilePath));
                return false;
            }

            if (owner is not null)
            {
                var message = L("Confirm_ImportFormMessage", sourceLabel, definePath,
                    string.Join("\n", plan.Files
                        .Where(f => f.Writes || f.Action == PlannedFileAction.Skip)
                        .Select(f => $"• {Path.GetRelativePath(definePath, f.Path)} ({FormWorkbookImport.ActionLabel(f.Action)})")));
                var warnings = issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
                if (warnings.Count > 0)
                    message += "\n\n" + L("Confirm_ImportFormWarnings", FormWorkbookImport.FormatIssues(warnings));
                if (!await ConfirmationDialog.ShowAsync(owner, L("Confirm_ImportFormTitle"), message, confirmLabel: L("Action_Import")))
                {
                    StatusText = L("Status_FormImportCancelled");
                    return false;
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

        if (imported is null) return false;
        onWritten?.Invoke();
        if (owner is not null && (GetOwnerWindow() ?? owner) is { } upgradeOwner)
            await OfferDatabaseUpgradeAsync(upgradeOwner, imported, importedTables);
        return true;
    }
}
