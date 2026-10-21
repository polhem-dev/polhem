using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>
    /// Tells the user what an import wrote and offers to upgrade the database next. The upgrade covers only
    /// <paramref name="tableNames"/>, the tables of the imported form, since an ERP database can hold thousands of tables.
    /// </summary>
    private async Task OfferDatabaseUpgradeAsync(Avalonia.Controls.Window owner, string imported, IReadOnlyList<string> tableNames)
    {
        var action = await ConfirmationDialog.ShowActionsAsync(owner, L("Imported_Title"),
            imported + "\n\n" + L("Imported_UpgradeHint", string.Join(", ", tableNames)),
            primaryLabel: L("Action_UpgradeDatabase"), secondaryLabel: L("Action_PreviewUpgrade"),
            closeLabel: L("Action_Close"));
        if (action is not null)
            await UpgradeDatabaseAsync(dryRun: action == 1, tableNames);
    }

    /// <summary>
    /// Builds or upgrades the database tables of the open solution with the database manager, or with
    /// <paramref name="dryRun"/> previews what it would change.
    /// </summary>
    /// <param name="dryRun">Whether to preview the changes only.</param>
    /// <param name="tableNames">The tables to upgrade; <see langword="null"/> or empty upgrades every registered table.</param>
    public Task UpgradeDatabaseAsync(bool dryRun, IReadOnlyList<string>? tableNames = null)
        => RunDbCommandAsync(dryRun ? DbCommandKind.Preview : DbCommandKind.Upgrade,
            DbManagerTool.UpgradeArguments(tableNames ?? [], dryRun));

    /// <summary>Opens a connection to each database of the open solution's DatabaseSettings with the database manager.</summary>
    public Task CheckDatabaseConnectionsAsync() => RunDbCommandAsync(DbCommandKind.Check, ["check"]);

    /// <summary>The database command window currently open, so a second request activates it instead of running twice.</summary>
    private DbCommandWindow? _dbCommandWindow;

    /// <summary>
    /// Runs a database manager command in the database command window. Unsaved edits are offered for saving first,
    /// since the tool reads the files on disk, and the first run asks before the tool is downloaded.
    /// </summary>
    private async Task RunDbCommandAsync(DbCommandKind kind, IReadOnlyList<string> arguments)
    {
        if (!IsSolutionOpened) return;
        var owner = GetOwnerWindow();
        if (owner is null) return;
        var definePath = SolutionPath;

        if (_dbCommandWindow is { IsRunning: true })
        {
            _dbCommandWindow.Activate();
            await ConfirmationDialog.ShowMessageAsync(_dbCommandWindow, L("DbCommand_BusyTitle"), L("DbCommand_BusyMessage"));
            return;
        }

        if (OpenDocuments.Any(d => d.IsDirty))
        {
            var choice = await ConfirmationDialog.ShowUnsavedAsync(owner, L("DbCommand_UnsavedTitle"), L("DbCommand_UnsavedMessage"),
                saveLabel: L("Action_SaveAll"), discardLabel: L("Action_ContinueWithoutSaving"), cancelLabel: L("Action_Cancel"));
            if (choice == ConfirmCloseResult.Cancel) return;
            if (choice == ConfirmCloseResult.Save) await SaveAll();
        }

        var extraSource = Environment.GetEnvironmentVariable(DbManagerTool.SourceVariable);
        if (!DbManagerTool.IsCached()
            && !await ConfirmationDialog.ShowAsync(owner, L("DbCommand_DownloadTitle"),
                L("DbCommand_DownloadMessage", DbManagerTool.PackageId, DbManagerTool.Version,
                    SecretMasker.Hide(string.IsNullOrWhiteSpace(extraSource)
                        ? DbManagerTool.NuGetSource
                        : DbManagerTool.NuGetSource + ", " + DbManagerTool.FullSource(extraSource))),
                confirmLabel: L("Action_Download")))
        {
            return;
        }

        string workDirectory;
        try
        {
            workDirectory = DbManagerTool.WorkDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = L("DbCommand_StatusStartFailed", ex.Message);
            return;
        }

        _dbCommandWindow?.Close();
        var command = DbManagerTool.Command(definePath, arguments, workDirectory, extraSource);
        _dbCommandWindow = DbCommandWindow.Start(owner, new DbCommandViewModel(command, definePath, kind));
    }
}
