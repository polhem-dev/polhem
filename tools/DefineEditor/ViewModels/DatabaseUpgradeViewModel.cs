using System.Text;
using Polhem.DefineEditor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Runs a solution's database upgrade command and collects what it writes, for the database upgrade window.
/// </summary>
/// <remarks>
/// The exit codes are those of the host's <c>db</c> commands (<c>Polhem.Hosting.Database.DatabaseCommandLine</c>):
/// 0 when every table went through, 1 when at least one failed, 2 for a command line the host did not understand.
/// DefineEditor does not reference <c>Polhem.Hosting</c>, which would bring the server side along.
/// </remarks>
public sealed partial class DatabaseUpgradeViewModel : ViewModelBase, IDisposable
{
    private const int UsageErrorExitCode = 2;

    private readonly ExternalCommand _command;
    private readonly string _baseDirectory;
    private readonly StringBuilder _output = new();
    private readonly CancellationTokenSource _cancellation = new();
    private bool _disposed;

    /// <param name="command">The command to run, with <c>--dry-run</c> already added for a preview.</param>
    /// <param name="baseDirectory">The folder a relative working directory is resolved against.</param>
    /// <param name="dryRun">Whether this run only previews the changes.</param>
    public DatabaseUpgradeViewModel(ExternalCommand command, string baseDirectory, bool dryRun)
    {
        _command = command;
        _baseDirectory = baseDirectory;
        IsDryRun = dryRun;
        _statusText = L("Upgrade_StatusRunning");
    }

    /// <summary>Whether this run only previews the changes.</summary>
    public bool IsDryRun { get; }

    /// <summary>
    /// The command line of one run: the solution's upgrade command with a <c>--table</c> option for each of
    /// <paramref name="tableNames"/> and, for a preview, <c>--dry-run</c>.
    /// </summary>
    public static ExternalCommand BuildRun(ExternalCommand command, IEnumerable<string> tableNames, bool dryRun)
    {
        var run = command;
        foreach (var tableName in tableNames)
            run = run.WithArgument("--table").WithArgument(tableName);
        return dryRun ? run.WithArgument("--dry-run") : run;
    }

    public string Title => L(IsDryRun ? "Upgrade_DryRunTitle" : "Upgrade_Title");

    /// <summary>The command line being run, with password values hidden.</summary>
    public string CommandLine => SecretMasker.Hide(_command.ToDisplayString());

    public string WorkingDirectory => _command.ResolveWorkingDirectory(_baseDirectory);

    /// <summary>Everything the command has written so far, one line per line.</summary>
    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionLabel))]
    private bool _isRunning;

    /// <summary>True once the command ended with exit code 0; false when it ended any other way.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailed))]
    private bool? _succeeded;

    public bool HasFailed => Succeeded == false;

    public string ActionLabel => L(IsRunning ? "Action_Cancel" : "Action_Close");

    /// <summary>Asks the window to close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The result of the run once it has ended.</summary>
    public ProcessRunResult? Result { get; private set; }

    /// <summary>Runs the command once and reports its lines and its outcome through the bound properties.</summary>
    public async Task RunAsync()
    {
        if (IsRunning || Result is not null) return;
        IsRunning = true;
        var context = SynchronizationContext.Current;
        try
        {
            Result = await ProcessRunner.RunAsync(_command, _baseDirectory, line =>
            {
                if (context is null) AppendLine(line);
                else context.Post(_ => AppendLine(line), null);
            }, _cancellation.Token);
        }
        finally
        {
            IsRunning = false;
        }

        Succeeded = Result.Succeeded;
        StatusText = Result switch
        {
            { Cancelled: true } => L("Upgrade_StatusCancelled"),
            { StartError: { } error } => L("Upgrade_StatusStartFailed", error),
            { ExitCode: 0 } => L(IsDryRun ? "Upgrade_StatusDryRunSucceeded" : "Upgrade_StatusSucceeded"),
            { ExitCode: UsageErrorExitCode } => L("Upgrade_StatusUsageError", UsageErrorExitCode),
            _ => L("Upgrade_StatusFailed", Result.ExitCode),
        };
    }

    private void AppendLine(string line)
    {
        lock (_output)
        {
            _output.AppendLine(line);
            OutputText = _output.ToString();
        }
    }

    /// <summary>Cancels a running command, or closes the window once it has ended.</summary>
    [RelayCommand]
    private void CancelOrClose()
    {
        if (IsRunning) Cancel();
        else CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Kills a running command; does nothing once it has ended.</summary>
    public void Cancel()
    {
        if (IsRunning && !_disposed) _cancellation.Cancel();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}
