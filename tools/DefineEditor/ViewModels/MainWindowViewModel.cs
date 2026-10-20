using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.Views;
using Polhem.Definition;
using Polhem.Definition.Spreadsheet;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<DefineNode> _nodes = new();

    /// <summary>The Programs view of the sidebar, built by <see cref="ProgramTreeBuilder"/>.</summary>
    [ObservableProperty]
    private ObservableCollection<DefineNode> _programNodes = new();

    /// <summary>The Databases view of the sidebar, built by <see cref="DbCategoryTreeBuilder"/>.</summary>
    [ObservableProperty]
    private ObservableCollection<DefineNode> _databaseNodes = new();

    /// <summary>The view the sidebar shows; picked from the activity bar and remembered in <see cref="UserSettings"/>.</summary>
    [ObservableProperty]
    private SidebarView _activeSidebarView = SidebarView.Explorer;

    /// <summary>False when the sidebar is collapsed by clicking the icon of the view it already shows.</summary>
    [ObservableProperty]
    private bool _isSidebarVisible = true;

    public bool IsExplorerView => ActiveSidebarView == SidebarView.Explorer;

    public bool IsProgramsView => ActiveSidebarView == SidebarView.Programs;

    public bool IsDatabasesView => ActiveSidebarView == SidebarView.Databases;

    /// <summary>Highlights the activity bar icon of the shown view; no icon is highlighted while the sidebar is collapsed.</summary>
    public bool IsExplorerActive => IsSidebarVisible && IsExplorerView;

    public bool IsProgramsActive => IsSidebarVisible && IsProgramsView;

    public bool IsDatabasesActive => IsSidebarVisible && IsDatabasesView;

    public bool HasNoProgramNodes => ProgramNodes.Count == 0;

    public bool HasNoDatabaseNodes => DatabaseNodes.Count == 0;

    /// <summary>
    /// Open document tabs. New tabs are appended; selecting an already-open
    /// node activates the existing tab rather than re-loading.
    /// </summary>
    public ObservableCollection<DocumentViewModelBase> OpenDocuments { get; } = new();

    [ObservableProperty]
    private DocumentViewModelBase? _activeDocument;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// Root path of the currently open solution. Empty when no solution is
    /// open — the welcome panel checks this to decide whether to show the
    /// "Open Folder" call-to-action vs the tree view.
    /// </summary>
    [ObservableProperty]
    private string _solutionPath = string.Empty;

    /// <summary>
    /// True when a solution is open. Derived from <see cref="SolutionPath"/>;
    /// bind <c>IsVisible</c> to this for "show tree" branches and to its
    /// negation for "show welcome" branches.
    /// </summary>
    public bool IsSolutionOpened => !string.IsNullOrEmpty(SolutionPath);

    [ObservableProperty]
    private DefineNode? _selectedNode;

    [ObservableProperty]
    private DefineNode? _selectedProgramNode;

    [ObservableProperty]
    private DefineNode? _selectedDatabaseNode;

    /// <summary>
    /// Solution-wide context (currently: the set of available FormSchema ProgIds)
    /// rebuilt each time a DefinePath is opened.
    /// </summary>
    public SolutionContext Solution { get; private set; } = SolutionContext.Empty;

    public bool HasOpenDocuments => OpenDocuments.Count > 0;

    public bool HasActiveDocument => ActiveDocument is not null;

    /// <summary>
    /// True when at least one open document has unsaved edits and supports
    /// saving. Drives the File menu's "Save All" availability; refreshed from
    /// each document's IsDirty change (see <see cref="OnOpenDocumentsChanged"/>).
    /// </summary>
    public bool HasDirtyDocuments =>
        OpenDocuments.Any(d => d.IsDirty && d.FileSaveCommand is not null);

    /// <summary>
    /// Active document's file path relative to <see cref="SolutionPath"/>, e.g.
    /// "FormSchema/Employee.xml". Empty when no document is active or the
    /// solution root is unknown. Bound to the status bar so the user can see
    /// which file they're editing at a glance — VS Code shows the same.
    /// </summary>
    public string ActiveDocumentRelativePath
    {
        get
        {
            if (ActiveDocument is null || string.IsNullOrEmpty(SolutionPath))
                return string.Empty;
            var key = ActiveDocument.DocumentKey;
            if (string.IsNullOrEmpty(key)) return string.Empty;
            try { return Path.GetRelativePath(SolutionPath, key); }
            catch (ArgumentException) { return key; }
        }
    }

    /// <summary>
    /// True when the active document has a backing file path to show in the
    /// status bar. False for path-less tabs (Welcome) so the "›" separator
    /// doesn't dangle with nothing after it.
    /// </summary>
    public bool HasActiveDocumentPath => ActiveDocumentRelativePath.Length > 0;

    /// <summary>
    /// Visibility hint for the right-pane "select a node to open a tab" welcome.
    /// Shown only when a solution is open but no document tab is active —
    /// otherwise the left-pane "Open Folder" welcome covers the empty state.
    /// </summary>
    public bool ShowDocumentWelcome => IsSolutionOpened && !HasOpenDocuments;

    public MainWindowViewModel()
    {
        OpenDocuments.CollectionChanged += OnOpenDocumentsChanged;
        if (Enum.TryParse<SidebarView>(UserSettings.Load().SidebarView, out var view))
            _activeSidebarView = view;
    }

    /// <summary>
    /// Shows <paramref name="view"/> in the sidebar, or collapses the sidebar when it already shows that view,
    /// as the VS Code activity bar does.
    /// </summary>
    [RelayCommand]
    private void ShowSidebarView(SidebarView view)
    {
        if (IsSidebarVisible && ActiveSidebarView == view)
        {
            IsSidebarVisible = false;
            return;
        }
        ActiveSidebarView = view;
        IsSidebarVisible = true;
    }

    partial void OnActiveSidebarViewChanged(SidebarView value)
    {
        OnPropertyChanged(nameof(IsExplorerView));
        OnPropertyChanged(nameof(IsProgramsView));
        OnPropertyChanged(nameof(IsDatabasesView));
        NotifyActivityBar();

        var settings = UserSettings.Load();
        settings.SidebarView = value.ToString();
        settings.Save();
    }

    partial void OnIsSidebarVisibleChanged(bool value) => NotifyActivityBar();

    private void NotifyActivityBar()
    {
        OnPropertyChanged(nameof(IsExplorerActive));
        OnPropertyChanged(nameof(IsProgramsActive));
        OnPropertyChanged(nameof(IsDatabasesActive));
    }

    partial void OnProgramNodesChanged(ObservableCollection<DefineNode> value)
        => OnPropertyChanged(nameof(HasNoProgramNodes));

    partial void OnDatabaseNodesChanged(ObservableCollection<DefineNode> value)
        => OnPropertyChanged(nameof(HasNoDatabaseNodes));

    /// <summary>
    /// Rebuilds every sidebar view from <paramref name="definePath"/>, or clears them when it is empty.
    /// </summary>
    private void RebuildTrees(string definePath)
    {
        if (string.IsNullOrEmpty(definePath))
        {
            Nodes = new ObservableCollection<DefineNode>();
            Solution = SolutionContext.Empty;
            ProgramNodes = new ObservableCollection<DefineNode>();
            DatabaseNodes = new ObservableCollection<DefineNode>();
            return;
        }
        var root = DefinePathScanner.Scan(definePath);
        Nodes = new ObservableCollection<DefineNode> { root };
        Solution = SolutionContext.FromTree(root);
        RebuildContentTrees(definePath);
    }

    /// <summary>Rebuilds the views that are built from file contents rather than from the folder layout.</summary>
    private void RebuildContentTrees(string definePath)
    {
        ProgramNodes = new ObservableCollection<DefineNode>(ProgramTreeBuilder.Build(definePath));
        DatabaseNodes = new ObservableCollection<DefineNode>(DbCategoryTreeBuilder.Build(definePath));
    }

    /// <summary>
    /// Documents whose PropertyChanged we are currently subscribed to for
    /// IsDirty tracking. Re-synced wholesale on every collection change —
    /// tab counts are small, and full resync sidesteps the Reset-action
    /// case where the removed items are not reported.
    /// </summary>
    private readonly List<DocumentViewModelBase> _dirtySubscriptions = new();

    private void OnOpenDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasOpenDocuments));
        OnPropertyChanged(nameof(ShowDocumentWelcome));

        foreach (var doc in _dirtySubscriptions)
        {
            doc.PropertyChanged -= OnDocumentPropertyChanged;
            doc.DefineFileGenerated -= OnDefineFileGenerated;
        }
        _dirtySubscriptions.Clear();
        foreach (var doc in OpenDocuments)
        {
            doc.PropertyChanged += OnDocumentPropertyChanged;
            doc.DefineFileGenerated += OnDefineFileGenerated;
            _dirtySubscriptions.Add(doc);
        }
        OnPropertyChanged(nameof(HasDirtyDocuments));
    }

    /// <summary>
    /// Rescans the solution so a newly generated define file appears in the tree, then opens it.
    /// </summary>
    private void OnDefineFileGenerated(object? sender, string filePath)
    {
        if (string.IsNullOrEmpty(SolutionPath)) return;

        RebuildTrees(SolutionPath);

        var node = FindNodeByFilePath(Nodes[0], filePath);
        if (node is not null) SelectedNode = node;
    }

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

        try
        {
            FormWorkbookReadResult read;
            using (var stream = File.OpenRead(workbookPath))
                read = FormWorkbookReader.Read(stream);

            var plan = read.HasErrors
                ? null
                : FormImporter.Plan(read.Content!, new FormImportOptions { DefinePath = definePath });

            // An existing TableSchema may carry what the form cannot derive (lengths, extra columns, indexes), so the
            // user picks how to treat it; the plan above used the default, Merge.
            if (plan is { HasErrors: false } && owner is not null
                && plan.Files.Any(f => IsTableSchema(f.Path) && File.Exists(f.Path)))
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
                if (mode != 0)
                {
                    // The conventions already ran on this content and change nothing when applied again.
                    plan = FormImporter.Plan(read.Content!, new FormImportOptions
                    {
                        DefinePath = definePath,
                        TableSchemaMode = mode == 1 ? TableSchemaImportMode.Keep : TableSchemaImportMode.Overwrite,
                    });
                }
            }
            var issues = read.Issues.Concat(plan?.Issues ?? []).ToList();
            if (plan is null || plan.HasErrors)
            {
                if (owner is not null)
                {
                    await ConfirmationDialog.ShowMessageAsync(owner, L("Error_ImportFormTitle"),
                        L("Error_ImportFormMessage", FormatIssues(issues)));
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
                        .Select(f => $"• {Path.GetRelativePath(definePath, f.Path)} ({ActionLabel(f.Action)})")));
                var warnings = issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
                if (warnings.Count > 0)
                    message += "\n\n" + L("Confirm_ImportFormWarnings", FormatIssues(warnings));
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
            RebuildTrees(definePath);
            // The plan lists the FormSchema first.
            var schemaNode = FindNodeByFilePath(Nodes[0], plan.Files[0].Path);
            if (schemaNode is not null) SelectedNode = schemaNode;
            StatusText = L("Status_FormImported", progId, written.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText = L("Status_FormImportFailed", ex.Message);
        }
    }

    private static string FormatIssues(IEnumerable<DefinitionIssue> issues)
        => string.Join("\n", issues.Select(i => $"• {i.Location}: {i.Message}"));

    private static string ActionLabel(PlannedFileAction action) => action switch
    {
        PlannedFileAction.Create => L("ImportAction_Create"),
        PlannedFileAction.Overwrite => L("ImportAction_Overwrite"),
        PlannedFileAction.Update => L("ImportAction_Update"),
        _ => L("ImportAction_Skip"),
    };

    private static bool IsTableSchema(string path)
        => path.EndsWith(".TableSchema.xml", StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static DefineNode? FindNodeByFilePath(DefineNode node, string filePath)
    {
        if (node.Kind == DefineNodeKind.DefineFile
            && string.Equals(node.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }
        foreach (var child in node.Children)
        {
            var found = FindNodeByFilePath(child, filePath);
            if (found is not null) return found;
        }
        return null;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DocumentViewModelBase.IsDirty)) return;
        OnPropertyChanged(nameof(HasDirtyDocuments));

        // A saved ProgramSettings, DbCategorySettings or FormSchema can change what the Programs and Databases views
        // list, so they are rebuilt when such a document turns clean.
        if (sender is ProgramSettingsDocumentViewModel or DbCategorySettingsDocumentViewModel or FormSchemaDocumentViewModel
            && sender is DocumentViewModelBase { IsDirty: false }
            && !string.IsNullOrEmpty(SolutionPath))
        {
            RebuildContentTrees(SolutionPath);
        }
    }

    partial void OnSolutionPathChanged(string value)
    {
        OnPropertyChanged(nameof(IsSolutionOpened));
        OnPropertyChanged(nameof(ShowDocumentWelcome));
        OnPropertyChanged(nameof(ActiveDocumentRelativePath));
        OnPropertyChanged(nameof(HasActiveDocumentPath));
    }

    partial void OnActiveDocumentChanged(DocumentViewModelBase? value)
    {
        OnPropertyChanged(nameof(HasActiveDocument));
        OnPropertyChanged(nameof(ActiveDocumentRelativePath));
        OnPropertyChanged(nameof(HasActiveDocumentPath));
    }

    /// <summary>
    /// Activates the Welcome tab, creating it (pinned as the first tab, like
    /// VS Code) when it isn't open. Called at startup when
    /// <see cref="UserSettings.ShowWelcomeOnStartup"/> is set and from
    /// View → Welcome.
    /// </summary>
    public void ShowWelcome()
    {
        var existing = OpenDocuments.OfType<WelcomeDocumentViewModel>().FirstOrDefault();
        if (existing is not null)
        {
            ActiveDocument = existing;
            return;
        }

        var doc = new WelcomeDocumentViewModel();
        OpenDocuments.Insert(0, doc);
        ActiveDocument = doc;
    }

    partial void OnSelectedNodeChanged(DefineNode? value) => OpenNode(value);

    partial void OnSelectedProgramNodeChanged(DefineNode? value) => OpenNode(value);

    partial void OnSelectedDatabaseNodeChanged(DefineNode? value) => OpenNode(value);

    /// <summary>
    /// Opens the define file of <paramref name="value"/> in a tab, or activates its tab when it is already open.
    /// The sidebar views share file paths, so a file opened from any of them lands in the same tab.
    /// </summary>
    private void OpenNode(DefineNode? value)
    {
        if (value is null
            || value.Kind != DefineNodeKind.DefineFile
            || string.IsNullOrEmpty(value.FilePath))
        {
            return;
        }

        // Already open? Activate that tab instead of creating a duplicate.
        var existing = OpenDocuments.FirstOrDefault(d =>
            string.Equals(d.DocumentKey, value.FilePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveDocument = existing;
            return;
        }

        var doc = DocumentViewModelFactory.Create(value, Solution);
        if (doc is null) return;
        OpenDocuments.Add(doc);
        ActiveDocument = doc;
    }

    [RelayCommand]
    private async Task CloseDocument(DocumentViewModelBase? doc)
    {
        if (doc is null) return;
        var idx = OpenDocuments.IndexOf(doc);
        if (idx < 0) return;
        if (!await PrepareCloseAsync(new List<DocumentViewModelBase> { doc })) return;

        OpenDocuments.Remove(doc);
        doc.Dispose();

        if (ActiveDocument == doc)
        {
            ActiveDocument = OpenDocuments.Count == 0
                ? null
                : OpenDocuments[Math.Min(idx, OpenDocuments.Count - 1)];
        }
    }

    [RelayCommand]
    private async Task CloseOtherDocuments(DocumentViewModelBase? doc)
    {
        if (doc is null) return;
        var targets = OpenDocuments.Where(d => d != doc).ToList();
        if (!await PrepareCloseAsync(targets)) return;
        CloseDocuments(targets, activate: doc);
    }

    [RelayCommand]
    private async Task CloseDocumentsToTheRight(DocumentViewModelBase? doc)
    {
        if (doc is null) return;
        var idx = OpenDocuments.IndexOf(doc);
        if (idx < 0) return;
        var targets = OpenDocuments.Skip(idx + 1).ToList();
        if (!await PrepareCloseAsync(targets)) return;
        CloseDocuments(targets, activate: doc);
    }

    // Only ever closes clean tabs, so no unsaved-changes prompt is needed.
    [RelayCommand]
    private void CloseSavedDocuments() =>
        CloseDocuments(OpenDocuments.Where(d => !d.IsDirty).ToList(), activate: ActiveDocument);

    [RelayCommand]
    private async Task CloseAllDocuments()
    {
        var targets = OpenDocuments.ToList();
        if (!await PrepareCloseAsync(targets)) return;
        CloseDocuments(targets, activate: null);
    }

    /// <summary>
    /// Gatekeeper run before any tab-close action. When <paramref name="docs"/>
    /// contains dirty documents, prompts Save / Don't Save / Cancel (VS Code
    /// style). "Save" runs each dirty editor's own Save flow — if any of them
    /// is backed out (validation-error prompt cancelled) the document stays
    /// dirty and the whole close is aborted so no edits are lost. Returns
    /// <c>true</c> when the caller may proceed with closing. Headless contexts
    /// (no main window) skip the prompt, consistent with the other dialogs.
    /// </summary>
    private static async Task<bool> PrepareCloseAsync(IReadOnlyList<DocumentViewModelBase> docs)
    {
        var dirty = docs.Where(d => d.IsDirty).ToList();
        if (dirty.Count == 0) return true;

        var owner = GetOwnerWindow();
        if (owner is null) return true;

        var message = dirty.Count == 1
            ? L("Confirm_CloseUnsavedMessage", dirty[0].Title)
            : L("Confirm_CloseUnsavedMessageMulti", dirty.Count,
                string.Join("\n", dirty.Select(d => "• " + d.Title)));

        var choice = await ConfirmationDialog.ShowUnsavedAsync(
            owner,
            L("Confirm_CloseUnsavedTitle"),
            message,
            saveLabel: L("Action_Save"),
            discardLabel: L("Action_DontSave"),
            cancelLabel: L("Action_Cancel"));

        switch (choice)
        {
            case ConfirmCloseResult.Discard:
                return true;
            case ConfirmCloseResult.Save:
                foreach (var doc in dirty)
                {
                    if (doc.FileSaveCommand is IAsyncRelayCommand asyncSave)
                        await asyncSave.ExecuteAsync(null);
                    else
                        doc.FileSaveCommand?.Execute(null);
                }
                return dirty.All(d => !d.IsDirty);
            default:
                return false;
        }
    }

    /// <summary>
    /// Removes and disposes <paramref name="docs"/>, then re-points
    /// <see cref="ActiveDocument"/>: prefer <paramref name="activate"/> when it
    /// survived the close, otherwise fall back to the last remaining tab (or
    /// null when none are left). Shared by the tab context-menu close actions.
    /// </summary>
    private void CloseDocuments(IReadOnlyList<DocumentViewModelBase> docs, DocumentViewModelBase? activate)
    {
        foreach (var doc in docs)
        {
            OpenDocuments.Remove(doc);
            doc.Dispose();
        }

        if (activate is not null && OpenDocuments.Contains(activate))
            ActiveDocument = activate;
        else if (ActiveDocument is null || !OpenDocuments.Contains(ActiveDocument))
            ActiveDocument = OpenDocuments.LastOrDefault();
    }

    /// <summary>
    /// Saves every dirty document that supports saving, reusing each editor's
    /// own Save flow (including the save-despite-validation-errors prompt — a
    /// cancelled prompt leaves that document dirty and the batch moves on).
    /// </summary>
    [RelayCommand]
    private async Task SaveAll()
    {
        var targets = OpenDocuments
            .Where(d => d.IsDirty && d.FileSaveCommand is not null)
            .ToList();
        if (targets.Count == 0) return;

        var saved = 0;
        foreach (var doc in targets)
        {
            if (doc.FileSaveCommand is IAsyncRelayCommand asyncSave)
                await asyncSave.ExecuteAsync(null);
            else
                doc.FileSaveCommand!.Execute(null);
            if (!doc.IsDirty) saved++;
        }
        StatusText = L("Status_SavedAll", saved, targets.Count);
    }

    /// <summary>
    /// Drops every open document tab and disposes the view-models so their
    /// <see cref="LocalizationService.CultureChanged"/> subscriptions release.
    /// Used by <see cref="OpenSolution"/> when switching solutions.
    /// </summary>
    private void DisposeAndClearOpenDocuments()
    {
        foreach (var doc in OpenDocuments) doc.Dispose();
        OpenDocuments.Clear();
    }

    /// <summary>
    /// Opens a DefinePath folder as the solution and rebuilds the tree. Any open
    /// document tabs from a previous solution are dropped.
    /// </summary>
    public void OpenSolution(string definePath)
    {
        try
        {
            // NOTE: opening only reads. A DefinePath may leave out the framework defaults it does not
            // use (Northwind has no UnitSettings or PermissionModels), and writing them on open dirtied
            // committed folders. Adding them is the explicit `AddMissingFrameworkDefaultsAsync` command.
            RebuildTrees(definePath);
            DisposeAndClearOpenDocuments();
            ActiveDocument = null;
            SelectedNode = null;
            SelectedProgramNode = null;
            SelectedDatabaseNode = null;

            // Record for File → Open Recent before assigning SolutionPath —
            // its PropertyChanged fires synchronously and the App-side menu
            // rebuild reads these settings in that handler.
            var settings = UserSettings.Load();
            settings.TouchRecentSolution(definePath);
            settings.Save();

            SolutionPath = definePath;

            StatusText = L("Status_SolutionLoaded", Solution.AvailableProgIds.Count);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            RebuildTrees(string.Empty);
            DisposeAndClearOpenDocuments();
            ActiveDocument = null;
            SelectedNode = null;
            SelectedProgramNode = null;
            SelectedDatabaseNode = null;
            SolutionPath = string.Empty;
            StatusText = L("Status_OpenSolutionFailed", ex.Message);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822",
        Justification = "RelayCommand handler bound from XAML must remain instance.")]
    [RelayCommand]
    private void ToggleTheme() => App.ToggleTheme();
}
