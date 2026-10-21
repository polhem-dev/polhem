using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>
    /// Tells the user what an import wrote and offers to run the solution's database upgrade command next, or says
    /// where to set that command when the solution has none. The upgrade covers only <paramref name="tableNames"/>,
    /// the tables of the imported form, since an ERP database can hold thousands of tables.
    /// </summary>
    private async Task OfferDatabaseUpgradeAsync(Avalonia.Controls.Window owner, string imported, IReadOnlyList<string> tableNames)
    {
        var command = TryLoadSolutionSettings()?.DatabaseUpgrade;
        if (command is not { IsConfigured: true })
        {
            await ConfirmationDialog.ShowMessageAsync(owner, L("Imported_Title"),
                imported + "\n\n" + L("Imported_NoUpgradeCommand", SolutionSettings.FileName), success: true);
            return;
        }

        var action = await ConfirmationDialog.ShowActionsAsync(owner, L("Imported_Title"),
            imported + "\n\n" + L("Imported_UpgradeHint", string.Join(", ", tableNames)),
            primaryLabel: L("Action_UpgradeDatabase"), secondaryLabel: L("Action_PreviewUpgrade"),
            closeLabel: L("Action_Close"));
        if (action is not null)
            await UpgradeDatabaseAsync(dryRun: action == 1, tableNames);
    }

    /// <summary>The database upgrade window currently open, so a second request activates it instead of running twice.</summary>
    private DatabaseUpgradeWindow? _upgradeWindow;

    /// <summary>
    /// Runs the solution's database upgrade command in the database upgrade window, or with <paramref name="dryRun"/>
    /// previews what it would change. Unsaved edits are offered for saving first, since the command reads the files.
    /// The first run of a command in a solution asks the user to approve it, because the command comes with the folder.
    /// </summary>
    /// <param name="dryRun">Whether to preview the changes only.</param>
    /// <param name="tableNames">The tables to upgrade; <see langword="null"/> or empty upgrades every registered table.</param>
    public async Task UpgradeDatabaseAsync(bool dryRun, IReadOnlyList<string>? tableNames = null)
    {
        if (!IsSolutionOpened) return;
        var owner = GetOwnerWindow();
        if (owner is null) return;
        var definePath = SolutionPath;

        if (_upgradeWindow is { IsRunning: true })
        {
            _upgradeWindow.Activate();
            return;
        }

        SolutionSettings settings;
        try
        {
            settings = SolutionSettings.Load(definePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText = L("Status_SolutionSettingsFailed", ex.Message);
            return;
        }
        var command = settings.DatabaseUpgrade;
        if (!command.IsConfigured)
        {
            await ConfirmationDialog.ShowMessageAsync(owner, L("Upgrade_Title"),
                L("Upgrade_NoCommand", SolutionSettings.FileName));
            return;
        }

        if (OpenDocuments.Any(d => d.IsDirty))
        {
            var choice = await ConfirmationDialog.ShowUnsavedAsync(owner, L("Upgrade_Title"), L("Upgrade_UnsavedMessage"),
                saveLabel: L("Action_SaveAll"), discardLabel: L("Action_ContinueWithoutSaving"), cancelLabel: L("Action_Cancel"));
            if (choice == ConfirmCloseResult.Cancel) return;
            if (choice == ConfirmCloseResult.Save) await SaveAll();
        }

        var fingerprint = command.Fingerprint(definePath);
        var userSettings = UserSettings.Load();
        if (!userSettings.IsCommandTrusted(fingerprint))
        {
            var approved = await ConfirmationDialog.ShowAsync(owner, L("Upgrade_TrustTitle"),
                L("Upgrade_TrustMessage", SolutionSettings.FileName,
                    SecretMasker.Hide(command.ToDisplayString()), command.ResolveWorkingDirectory(definePath)),
                confirmLabel: L("Action_Run"));
            if (!approved) return;
            userSettings.TrustCommand(fingerprint);
            userSettings.Save();
        }

        _upgradeWindow?.Close();
        var run = DatabaseUpgradeViewModel.BuildRun(command, tableNames ?? [], dryRun);
        _upgradeWindow = DatabaseUpgradeWindow.Start(owner, new DatabaseUpgradeViewModel(run, definePath, dryRun));
    }

    /// <summary>Edits the open solution's <see cref="SolutionSettings"/> in the Solution Settings dialog.</summary>
    public async Task EditSolutionSettingsAsync()
    {
        if (!IsSolutionOpened) return;
        var owner = GetOwnerWindow();
        if (owner is null) return;
        var definePath = SolutionPath;

        try
        {
            var settings = SolutionSettings.Load(definePath);
            if (!await SolutionSettingsDialog.ShowAsync(owner, new SolutionSettingsDialogViewModel(settings))) return;
            settings.Save(definePath);
            StatusText = L("Status_SolutionSettingsSaved", SolutionSettings.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText = L("Status_SolutionSettingsFailed", ex.Message);
        }
    }

    private SolutionSettings? TryLoadSolutionSettings()
    {
        try
        {
            return SolutionSettings.Load(SolutionPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText = L("Status_SolutionSettingsFailed", ex.Message);
            return null;
        }
    }
}
