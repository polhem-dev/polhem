using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel : IImportBatchHost
{
    /// <summary>The batch import wizard currently open, so a second request activates it instead of opening another.</summary>
    private ImportBatchWindow? _importBatchWindow;

    /// <summary>Where the database manager runs for the batch import, resolved once before the first workbook.</summary>
    private DbManagerLaunch? _importBatchLaunch;

    /// <summary>Opens the batch import wizard over the main window, or brings the open one to the front.</summary>
    public void OpenImportBatch()
    {
        if (!IsSolutionOpened) return;
        if (_importBatchWindow is not null)
        {
            _importBatchWindow.Activate();
            return;
        }
        var owner = GetOwnerWindow();
        if (owner is null) return;

        _importBatchWindow = ImportBatchWindow.Open(owner, new ImportBatchViewModel(SolutionPath, this));
        _importBatchWindow.Closed += (_, _) => _importBatchWindow = null;
    }

    async Task<bool> IImportBatchHost.PrepareAsync(bool upgradeDatabase)
    {
        // The dialogs open over the wizard, which stays in front of the main window.
        var owner = (Avalonia.Controls.Window?)_importBatchWindow ?? GetOwnerWindow();
        if (owner is null) return !upgradeDatabase;

        if (_dbCommandWindow is { IsRunning: true })
        {
            await ConfirmationDialog.ShowMessageAsync(owner, L("DbCommand_BusyTitle"), L("DbCommand_BusyMessage"));
            return false;
        }

        // An open editor holds its own copy of a file the batch may replace, so unsaved edits are saved first rather
        // than lost on one side or the other.
        if (OpenDocuments.Any(d => d.IsDirty))
        {
            if (!await ConfirmationDialog.ShowAsync(owner, L("ImportBatch_UnsavedTitle"), L("ImportBatch_UnsavedMessage"),
                    confirmLabel: L("Action_SaveAll")))
            {
                return false;
            }
            await SaveAll();
            if (OpenDocuments.Any(d => d.IsDirty)) return false;
        }

        if (!upgradeDatabase) return true;
        _importBatchLaunch = await ResolveDbManagerAsync(owner);
        return _importBatchLaunch is not null;
    }

    Task<ProcessRunResult> IImportBatchHost.UpgradeTablesAsync(
        IReadOnlyList<string> tableNames, Action<string> onLine, CancellationToken cancellationToken)
    {
        if (_importBatchLaunch is null)
            return Task.FromResult(new ProcessRunResult(null, L("ImportBatch_NoDbManager"), false));

        var command = DbManagerTool.Command(SolutionPath, DbManagerTool.UpgradeArguments(tableNames, dryRun: false),
            _importBatchLaunch.WorkDirectory, _importBatchLaunch.ExtraSource);
        onLine("$ " + SecretMasker.Hide(command.ToDisplayString()));
        return ProcessRunner.RunAsync(command, SolutionPath, onLine, cancellationToken);
    }

    void IImportBatchHost.Imported(IReadOnlyList<string> writtenFiles, string? lastSchemaPath)
    {
        CloseDocuments(OpenDocuments
            .OfType<ObjectTreeDocumentViewModelBase>()
            .Where(d => writtenFiles.Any(f => SamePath(f, d.FilePath)))
            .ToList<DocumentViewModelBase>(), ActiveDocument);
        RebuildTrees(SolutionPath, revealFilePath: lastSchemaPath);
        if (lastSchemaPath is not null && FindNodeByFilePath(Nodes[0], lastSchemaPath) is { } schemaNode)
            SelectedNode = schemaNode;
        StatusText = L("Status_BatchImported", writtenFiles.Count);
    }
}
