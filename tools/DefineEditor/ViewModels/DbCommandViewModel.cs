using System.Text;
using Polhem.DefineEditor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Runs a database manager command and collects what it writes, for the database command window.
/// </summary>
/// <remarks>
/// The exit codes are those of the <c>db</c> commands (<c>Polhem.Hosting.Database.DatabaseCommandLine</c>), which
/// <see cref="DbManagerTool"/> runs: 0 when every item went through, 1 when at least one failed, 2 for a command line
/// that was not understood. DefineEditor does not reference <c>Polhem.Hosting</c>, which would bring the server side
/// along.
/// </remarks>
public sealed partial class DbCommandViewModel : ViewModelBase, IDisposable
{
    private const int UsageErrorExitCode = 2;

    private readonly ExternalCommand _command;
    private readonly string _baseDirectory;
    private readonly StringBuilder _output = new();
    private readonly CancellationTokenSource _cancellation = new();
    private bool _disposed;
    private bool _flushPending;
    private SynchronizationContext? _context;

    /// <param name="command">The command to run.</param>
    /// <param name="baseDirectory">The folder a relative working directory is resolved against.</param>
    /// <param name="kind">What the command does.</param>
    public DbCommandViewModel(ExternalCommand command, string baseDirectory, DbCommandKind kind)
    {
        _command = command;
        _baseDirectory = baseDirectory;
        Kind = kind;
        _statusText = L("DbCommand_StatusRunning");
    }

    /// <summary>What the command does.</summary>
    public DbCommandKind Kind { get; }

    public string Title => L(Kind switch
    {
        DbCommandKind.Preview => "DbCommand_PreviewTitle",
        DbCommandKind.Check => "DbCommand_CheckTitle",
        _ => "DbCommand_UpgradeTitle",
    });

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
        _context = SynchronizationContext.Current;
        try
        {
            Result = await ProcessRunner.RunAsync(_command, _baseDirectory, AppendLine, _cancellation.Token);
        }
        finally
        {
            IsRunning = false;
        }
        Flush();

        Succeeded = Result.Succeeded;
        StatusText = Result switch
        {
            { Cancelled: true } => L("DbCommand_StatusCancelled"),
            { StartError: { } error } => L("DbCommand_StatusStartFailed", error),
            { ExitCode: 0 } => L(Kind switch
            {
                DbCommandKind.Preview => "DbCommand_StatusPreviewSucceeded",
                DbCommandKind.Check => "DbCommand_StatusCheckSucceeded",
                _ => "DbCommand_StatusUpgradeSucceeded",
            }),
            { ExitCode: UsageErrorExitCode } => L("DbCommand_StatusUsageError", UsageErrorExitCode),
            _ => L("DbCommand_StatusFailed", Result.ExitCode),
        };
    }

    private void AppendLine(string line)
    {
        lock (_output)
        {
            _output.AppendLine(line);
            // Lines that arrive together are shown in one update: rebuilding the text for each line of a long upgrade
            // would cost time in proportion to the square of its length.
            if (_flushPending) return;
            _flushPending = true;
        }
        if (_context is null) Flush();
        else _context.Post(_ => Flush(), null);
    }

    private void Flush()
    {
        lock (_output)
        {
            _flushPending = false;
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
