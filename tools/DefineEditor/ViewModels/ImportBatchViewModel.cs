using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The batch import wizard: several form workbooks are imported one after another, each followed by an upgrade of its
/// own tables, and the batch stops at the first workbook that cannot be imported or upgraded.
/// </summary>
/// <remarks>
/// Each workbook is planned only after the one before it was written, since a form may relate to a form imported
/// earlier in the same batch. What was imported before a failure stays; going back from the last step lists the
/// workbooks still to import.
/// </remarks>
public sealed partial class ImportBatchViewModel : ViewModelBase
{
    private readonly string _definePath;
    private readonly IImportBatchHost _host;
    private readonly StringBuilder _output = new();
    private CancellationTokenSource? _cancellation;
    private SynchronizationContext? _context;
    private bool _flushPending;

    /// <param name="definePath">The DefinePath of the open solution.</param>
    /// <param name="host">The main window's part of the work.</param>
    public ImportBatchViewModel(string definePath, IImportBatchHost host)
    {
        _definePath = definePath;
        _host = host;
        Files.CollectionChanged += (_, _) => NotifyNavigation();
    }

    /// <summary>The workbooks, in the order they are imported.</summary>
    public ObservableCollection<ImportBatchItem> Files { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(RemoveCommand))]
    private ImportBatchItem? _selectedFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilesStep), nameof(IsOptionsStep), nameof(IsConfirmStep), nameof(IsRunStep),
        nameof(StepTitle), nameof(ConfirmText))]
    private ImportBatchStep _step;

    public bool IsFilesStep => Step == ImportBatchStep.Files;
    public bool IsOptionsStep => Step == ImportBatchStep.Options;
    public bool IsConfirmStep => Step == ImportBatchStep.Confirm;
    public bool IsRunStep => Step == ImportBatchStep.Run;

    /// <summary>The step number and its title, such as "Step 2 of 4: Import options".</summary>
    public string StepTitle => L("ImportBatch_Step" + Step);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeTableSchemas), nameof(KeepTableSchemas), nameof(OverwriteTableSchemas))]
    private TableSchemaImportMode _tableSchemaMode = TableSchemaImportMode.Merge;

    public bool MergeTableSchemas
    {
        get => TableSchemaMode == TableSchemaImportMode.Merge;
        set { if (value) TableSchemaMode = TableSchemaImportMode.Merge; }
    }

    public bool KeepTableSchemas
    {
        get => TableSchemaMode == TableSchemaImportMode.Keep;
        set { if (value) TableSchemaMode = TableSchemaImportMode.Keep; }
    }

    public bool OverwriteTableSchemas
    {
        get => TableSchemaMode == TableSchemaImportMode.Overwrite;
        set { if (value) TableSchemaMode = TableSchemaImportMode.Overwrite; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeepLayouts))]
    private bool _regenerateLayouts;

    public bool KeepLayouts
    {
        get => !RegenerateLayouts;
        set => RegenerateLayouts = !value;
    }

    /// <summary>Whether each import is followed by an upgrade of that form's tables.</summary>
    [ObservableProperty]
    private bool _upgradeDatabase = true;

    /// <summary>The order and the options, for the confirmation step.</summary>
    public string ConfirmText => L("ImportBatch_ConfirmMessage",
        Files.Count, _definePath,
        string.Join("\n", Files.Select((f, i) => $"{i + 1}. {f.FileName}")),
        L("ImportBatch_TableSchema" + TableSchemaMode),
        L(RegenerateLayouts ? "ImportBatch_LayoutRegenerate" : "ImportBatch_LayoutKeep"),
        L(UpgradeDatabase ? "ImportBatch_UpgradeYes" : "ImportBatch_UpgradeNo"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelLabel))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelLabel))]
    private bool _isFinished;

    /// <summary>True when the batch stopped before every workbook was imported.</summary>
    [ObservableProperty]
    private bool _hasFailed;

    [ObservableProperty]
    private bool _isStopRequested;

    /// <summary>Everything the batch reported so far: the files each import wrote, the issues, the upgrade output.</summary>
    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public string NextLabel => L(Step == ImportBatchStep.Confirm ? "Action_Start" : "Action_Next");

    public bool IsNextVisible => Step != ImportBatchStep.Run;

    public string CancelLabel => L(IsRunning ? "Action_StopAfterCurrent" : IsFinished ? "Action_Close" : "Action_Cancel");

    /// <summary>Asks the window to close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Adds workbooks the list does not have yet, keeping the list in file name order.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        var known = Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in paths.Where(p => known.Add(p)))
        {
            var item = new ImportBatchItem(path);
            var index = 0;
            while (index < Files.Count
                && StringComparer.OrdinalIgnoreCase.Compare(Files[index].FileName, item.FileName) <= 0)
            {
                index++;
            }
            Files.Insert(index, item);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    private bool CanMoveUp() => SelectedFile is not null && Files.IndexOf(SelectedFile) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    private bool CanMoveDown() => SelectedFile is not null && Files.IndexOf(SelectedFile) < Files.Count - 1;

    private void Move(int offset)
    {
        var item = SelectedFile!;
        var index = Files.IndexOf(item);
        Files.Move(index, index + offset);
        SelectedFile = item;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        var index = Files.IndexOf(SelectedFile!);
        Files.RemoveAt(index);
        SelectedFile = Files.Count == 0 ? null : Files[Math.Min(index, Files.Count - 1)];
    }

    private bool CanRemove() => SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (Step == ImportBatchStep.Run)
        {
            // The workbooks already imported are done; the list keeps the failed one and those after it, so fixing
            // the workbook and starting again carries on where the batch stopped.
            foreach (var done in Files.Where(f => f.Status == ImportBatchItemStatus.Done).ToList())
                Files.Remove(done);
            foreach (var item in Files) item.Status = ImportBatchItemStatus.Pending;
            IsFinished = false;
            HasFailed = false;
            StatusText = string.Empty;
            SetOutput(string.Empty);
            Step = ImportBatchStep.Files;
        }
        else
        {
            Step -= 1;
        }
        NotifyNavigation();
    }

    /// <summary>
    /// True before the run, except on the first step, and after a run that stopped early. A finished run cannot be
    /// stepped back into, and a running one cannot be left.
    /// </summary>
    public bool CanGoBack() => Step switch
    {
        ImportBatchStep.Files => false,
        ImportBatchStep.Run => IsFinished && HasFailed,
        _ => true,
    };

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        if (Step == ImportBatchStep.Confirm)
        {
            await RunAsync();
            return;
        }
        Step += 1;
        NotifyNavigation();
    }

    public bool CanGoNext() => Step switch
    {
        ImportBatchStep.Files => Files.Count > 0,
        ImportBatchStep.Run => false,
        _ => true,
    };

    /// <summary>Cancels the wizard, asks a running batch to stop after the current workbook, or closes the window.</summary>
    [RelayCommand]
    private void CancelOrClose()
    {
        if (IsRunning) RequestStop();
        else CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Lets the workbook being imported finish, upgrade included, and stops before the next one. A half-run upgrade
    /// is not interrupted.
    /// </summary>
    public void RequestStop()
    {
        if (!IsRunning || IsStopRequested) return;
        IsStopRequested = true;
        StatusText = L("ImportBatch_Stopping");
    }

    /// <summary>Imports the listed workbooks one by one; see the class remarks for when it stops.</summary>
    public async Task RunAsync()
    {
        if (IsRunning || Files.Count == 0) return;
        if (!await _host.PrepareAsync(UpgradeDatabase)) return;

        _context = SynchronizationContext.Current;
        _cancellation = new CancellationTokenSource();
        foreach (var item in Files) item.Status = ImportBatchItemStatus.Pending;
        SetOutput(string.Empty);
        IsStopRequested = false;
        HasFailed = false;
        IsFinished = false;
        IsRunning = true;
        Step = ImportBatchStep.Run;
        NotifyNavigation();

        var options = new FormImportOptions
        {
            DefinePath = _definePath,
            TableSchemaMode = TableSchemaMode,
            LayoutMode = RegenerateLayouts ? FormLayoutImportMode.Regenerate : FormLayoutImportMode.Keep,
        };
        var written = new List<string>();
        string? lastSchemaPath = null;
        ImportBatchItem? failed = null;
        var imported = 0;

        foreach (var item in Files.ToList())
        {
            if (IsStopRequested) break;
            StatusText = L("ImportBatch_Importing", item.FileName);
            var result = await ImportOneAsync(item, options, _cancellation.Token);
            if (result is null)
            {
                failed = item;
                break;
            }
            written.AddRange(result.Value.Written);
            lastSchemaPath = result.Value.SchemaPath;
            imported++;
        }

        foreach (var item in Files.Where(f => f.Status == ImportBatchItemStatus.Pending))
            item.Status = ImportBatchItemStatus.NotRun;
        Flush();

        HasFailed = failed is not null || imported < Files.Count;
        StatusText = failed is not null
            ? L("ImportBatch_RunFailed", failed.FileName)
            : HasFailed
                ? L("ImportBatch_RunStopped", imported, Files.Count)
                : L("ImportBatch_RunSucceeded", imported);
        IsRunning = false;
        IsFinished = true;
        _cancellation.Dispose();
        _cancellation = null;
        NotifyNavigation();

        if (lastSchemaPath is not null) _host.Imported(written, lastSchemaPath);
    }

    /// <summary>Imports one workbook and upgrades its tables; null when the batch has to stop here.</summary>
    private async Task<(IReadOnlyList<string> Written, string SchemaPath)?> ImportOneAsync(
        ImportBatchItem item, FormImportOptions options, CancellationToken cancellationToken)
    {
        item.Status = ImportBatchItemStatus.Importing;
        AppendLine($"── {item.FileName}");

        FormImportPlan plan;
        List<PlannedFile> written;
        try
        {
            var prepared = await Task.Run(() => FormWorkbookImport.Prepare(item.Path, options));
            if (prepared.Issues.Count > 0) AppendLine(FormWorkbookImport.FormatIssues(prepared.Issues));
            if (prepared.HasErrors)
            {
                AppendLine(L("ImportBatch_HasErrors"));
                item.Status = ImportBatchItemStatus.Failed;
                return null;
            }

            plan = prepared.Plan!;
            written = plan.Files.Where(f => f.Writes).ToList();
            if (written.Count == 0)
            {
                AppendLine(L("ImportBatch_Unchanged"));
            }
            else
            {
                await Task.Run(() => FormImporter.Apply(plan));
                AppendLine(L("ImportBatch_Written", written.Count));
                foreach (var file in written)
                    AppendLine($"• {Path.GetRelativePath(_definePath, file.Path)} ({FormWorkbookImport.ActionLabel(file.Action)})");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            AppendLine(ex.Message);
            item.Status = ImportBatchItemStatus.Failed;
            return null;
        }

        if (UpgradeDatabase)
        {
            item.Status = ImportBatchItemStatus.Upgrading;
            var result = await _host.UpgradeTablesAsync(FormWorkbookImport.TableNames(plan), AppendLine, cancellationToken);
            Flush();
            if (!result.Succeeded)
            {
                AppendLine(result switch
                {
                    { Cancelled: true } => L("DbCommand_StatusCancelled"),
                    { StartError: { } error } => L("DbCommand_StatusStartFailed", error),
                    _ => L("DbCommand_StatusFailed", result.ExitCode),
                });
                item.Status = ImportBatchItemStatus.Failed;
                return null;
            }
        }

        item.Status = ImportBatchItemStatus.Done;
        // The plan lists the FormSchema first.
        return (written.Select(f => f.Path).ToList(), plan.Files[0].Path);
    }

    /// <summary>Cancels a running batch outright, upgrade included; for the window closing.</summary>
    public void Abort() => _cancellation?.Cancel();

    private void AppendLine(string line)
    {
        lock (_output)
        {
            _output.AppendLine(line);
            // Lines that arrive together are shown in one update, as in the database command window.
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

    private void SetOutput(string text)
    {
        lock (_output)
        {
            _output.Clear().Append(text);
            OutputText = text;
        }
    }

    private void NotifyNavigation()
    {
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(IsNextVisible));
        OnPropertyChanged(nameof(ConfirmText));
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }
}
