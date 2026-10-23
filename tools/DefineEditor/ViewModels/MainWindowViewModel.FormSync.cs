using CommunityToolkit.Mvvm.Input;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel
{
    private void OnFormSchemaActionRequested(object? sender, FormSchemaAction action)
    {
        if (sender is not FormSchemaDocumentViewModel document) return;
        _ = action switch
        {
            FormSchemaAction.SyncDefinitions => SyncFormDefinitionsAsync(document),
            FormSchemaAction.UpgradeTables => UpgradeFormTablesAsync(document, dryRun: false),
            _ => UpgradeFormTablesAsync(document, dryRun: true),
        };
    }

    /// <summary>
    /// Brings the TableSchemas, the FormLayout and the table registrations of the form in <paramref name="document"/> up
    /// to date with it: shows the files the sync would change and what it cannot change, writes them once the user
    /// confirms, and offers to upgrade the form's tables next.
    /// </summary>
    /// <remarks>
    /// The sync reads the saved FormSchema, so unsaved edits are saved first. It only adds; see <see cref="FormSync"/>.
    /// </remarks>
    public async Task SyncFormDefinitionsAsync(FormSchemaDocumentViewModel document)
    {
        if (!IsSolutionOpened) return;
        var definePath = SolutionPath;
        var owner = GetOwnerWindow();
        var progId = document.Schema.ProgId;
        string? synced = null;
        IReadOnlyList<string> tableNames = [];

        if (document.IsDirty)
        {
            if (owner is not null && !await ConfirmationDialog.ShowAsync(owner, L("Confirm_SyncFormTitle"),
                    L("Sync_SaveFirst", progId), confirmLabel: L("Action_SaveAndContinue")))
            {
                StatusText = L("Status_FormSyncCancelled");
                return;
            }
            if (document.FileSaveCommand is IAsyncRelayCommand save) await save.ExecuteAsync(null);
            if (document.IsDirty)
            {
                StatusText = L("Status_FormSyncCancelled");
                return;
            }
        }

        try
        {
            var plan = FormSync.Plan(definePath, progId);
            if (plan.HasErrors)
            {
                if (owner is not null)
                {
                    await ConfirmationDialog.ShowMessageAsync(owner, L("Error_SyncFormTitle"),
                        L("Error_SyncFormMessage", FormWorkbookImport.FormatIssues(plan.Issues)));
                }
                StatusText = L("Status_FormSyncCancelled");
                return;
            }

            // A layout field removed by hand on purpose would come back on every sync, so the user can keep the layout.
            if (owner is not null && plan.Files.Any(f => FormWorkbookImport.IsFormLayout(f.Path) && f.Action == PlannedFileAction.Update))
            {
                var layout = await ConfirmationDialog.ShowChoiceAsync(owner, L("Confirm_SyncFormTitle"),
                    L("Choice_SyncFormLayoutMessage"),
                    [L("Choice_FormLayoutMerge"), L("Choice_FormLayoutKeep")],
                    selectedIndex: 0, confirmLabel: L("Action_Next"));
                if (layout is null)
                {
                    StatusText = L("Status_FormSyncCancelled");
                    return;
                }
                if (layout == 1) plan = FormSync.Plan(definePath, progId, FormLayoutImportMode.Keep);
            }

            var warnings = plan.Issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
            var written = plan.Files.Where(f => f.Writes).ToList();
            if (written.Count == 0)
            {
                if (owner is not null && warnings.Count > 0)
                {
                    await ConfirmationDialog.ShowMessageAsync(owner, L("Confirm_SyncFormTitle"),
                        L("Sync_UpToDateWithWarnings", progId, FormWorkbookImport.FormatIssues(warnings)));
                }
                StatusText = L("Status_FormSyncUnchanged", progId);
                return;
            }

            var dirty = OpenDocuments
                .OfType<ObjectTreeDocumentViewModelBase>()
                .FirstOrDefault(d => d.IsDirty && written.Any(f => SamePath(f.Path, d.FilePath)));
            if (dirty is not null)
            {
                StatusText = L("Status_FormSyncBlockedByDirty", Path.GetFileName(dirty.FilePath));
                return;
            }

            var files = FormSchemaSync.DescribeWrites(plan, definePath);
            if (owner is not null)
            {
                var message = L("Confirm_SyncFormMessage", progId, definePath, files);
                if (warnings.Count > 0)
                    message += "\n\n" + L("Sync_ManualAttention", FormWorkbookImport.FormatIssues(warnings));
                if (!await ConfirmationDialog.ShowAsync(owner, L("Confirm_SyncFormTitle"), message, confirmLabel: L("Action_Sync")))
                {
                    StatusText = L("Status_FormSyncCancelled");
                    return;
                }
            }

            FormImporter.Apply(plan);
            CloseDocuments(OpenDocuments
                .OfType<ObjectTreeDocumentViewModelBase>()
                .Where(d => written.Any(f => SamePath(f.Path, d.FilePath)))
                .ToList<DocumentViewModelBase>(), ActiveDocument);
            RebuildTrees(definePath, revealFilePath: document.FilePath);
            StatusText = L("Status_FormSynced", progId, written.Count);
            synced = L("Status_FormSynced", progId, written.Count) + "\n" + files;
            tableNames = FormSync.TableNames(document.Schema);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            StatusText = L("Status_FormSyncFailed", ex.Message);
        }

        if (synced is not null && owner is not null)
            await OfferDatabaseUpgradeAsync(owner, synced, tableNames, "Synced_Title", "Synced_UpgradeHint");
    }

    /// <summary>
    /// Upgrades, or with <paramref name="dryRun"/> previews the upgrade of, the database tables of the form in
    /// <paramref name="document"/>: its master table and its detail tables (<see cref="FormSync.TableNames"/>).
    /// </summary>
    public async Task UpgradeFormTablesAsync(FormSchemaDocumentViewModel document, bool dryRun)
    {
        if (!IsSolutionOpened) return;
        var tableNames = FormSync.TableNames(document.Schema);
        if (tableNames.Count == 0) return;

        // The database manager upgrades only the tables DbCategorySettings registers, and would pass over the others.
        var unregistered = FormSchemaSync.UnregisteredTables(SolutionPath, document.Schema);
        if (unregistered.Count > 0)
        {
            var owner = GetOwnerWindow();
            if (owner is not null)
            {
                await ConfirmationDialog.ShowMessageAsync(owner, L("Error_UpgradeFormTablesTitle"),
                    L("Sync_UnregisteredTables", string.Join(", ", unregistered)));
            }
            StatusText = L("Status_FormTablesUnregistered", string.Join(", ", unregistered));
            return;
        }

        await UpgradeDatabaseAsync(dryRun, tableNames);
    }
}
