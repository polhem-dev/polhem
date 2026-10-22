using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// What the batch import wizard needs from the main window: the checks before it starts, the database upgrade, and
/// refreshing the solution afterwards. Tests supply their own.
/// </summary>
public interface IImportBatchHost
{
    /// <summary>
    /// Runs the checks before the first workbook: unsaved edits are saved, and the database manager is available when
    /// <paramref name="upgradeDatabase"/> is set. Returns false when the user declined.
    /// </summary>
    Task<bool> PrepareAsync(bool upgradeDatabase);

    /// <summary>Builds or upgrades <paramref name="tableNames"/>, reporting each line of output.</summary>
    Task<ProcessRunResult> UpgradeTablesAsync(
        IReadOnlyList<string> tableNames, Action<string> onLine, CancellationToken cancellationToken);

    /// <summary>
    /// Called once the batch ends with the files it wrote, so open editors of those files close and the trees reload.
    /// </summary>
    /// <param name="writtenFiles">The full paths of the files written.</param>
    /// <param name="lastSchemaPath">The FormSchema of the last workbook imported, to select; <see langword="null"/> when none was.</param>
    void Imported(IReadOnlyList<string> writtenFiles, string? lastSchemaPath);
}
