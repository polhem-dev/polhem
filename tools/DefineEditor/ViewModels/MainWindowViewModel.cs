using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
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

    /// <summary>The Table Schemas view of the sidebar, built by <see cref="DbCategoryTreeBuilder"/>.</summary>
    [ObservableProperty]
    private ObservableCollection<DefineNode> _tableSchemaNodes = new();

    /// <summary>The view the sidebar shows; picked from the activity bar and remembered in <see cref="UserSettings"/>.</summary>
    [ObservableProperty]
    private SidebarView _activeSidebarView = SidebarView.Explorer;

    /// <summary>False when the sidebar is collapsed by clicking the icon of the view it already shows.</summary>
    [ObservableProperty]
    private bool _isSidebarVisible = true;

    public bool IsExplorerView => ActiveSidebarView == SidebarView.Explorer;

    public bool IsProgramsView => ActiveSidebarView == SidebarView.Programs;

    public bool IsTableSchemasView => ActiveSidebarView == SidebarView.TableSchemas;

    /// <summary>Highlights the activity bar icon of the shown view; no icon is highlighted while the sidebar is collapsed.</summary>
    public bool IsExplorerActive => IsSidebarVisible && IsExplorerView;

    public bool IsProgramsActive => IsSidebarVisible && IsProgramsView;

    public bool IsTableSchemasActive => IsSidebarVisible && IsTableSchemasView;

    public bool HasNoProgramNodes => ProgramNodes.Count == 0;

    public bool HasNoTableSchemaNodes => TableSchemaNodes.Count == 0;

    /// <summary>
    /// Open document tabs. New tabs are appended; selecting an already-open
    /// node activates the existing tab rather than re-loading.
    /// </summary>
    public ObservableCollection<DocumentViewModelBase> OpenDocuments { get; } = new();

    /// <summary>
    /// Raised after the persisted recent-solutions list changes, so the
    /// File → Open Recent menu and an open Welcome tab can refresh.
    /// </summary>
    public event EventHandler? RecentSolutionsChanged;

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
    private DefineNode? _selectedTableSchemaNode;

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

    /// <summary>
    /// Collapses the sidebar, or brings it back with the view it showed last, as VS Code's View → Toggle Side Bar does.
    /// </summary>
    [RelayCommand]
    private void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

    partial void OnActiveSidebarViewChanged(SidebarView value)
    {
        OnPropertyChanged(nameof(IsExplorerView));
        OnPropertyChanged(nameof(IsProgramsView));
        OnPropertyChanged(nameof(IsTableSchemasView));
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
        OnPropertyChanged(nameof(IsTableSchemasActive));
    }

    partial void OnProgramNodesChanged(ObservableCollection<DefineNode> value)
        => OnPropertyChanged(nameof(HasNoProgramNodes));

    partial void OnTableSchemaNodesChanged(ObservableCollection<DefineNode> value)
        => OnPropertyChanged(nameof(HasNoTableSchemaNodes));

    /// <summary>
    /// Rebuilds every sidebar view from <paramref name="definePath"/>, or clears them when it is empty.
    /// </summary>
    /// <remarks>
    /// The nodes expanded before stay expanded when the same solution is rebuilt, and the ancestors of
    /// <paramref name="revealFilePath"/> are expanded so the file about to be selected is in view.
    /// </remarks>
    private void RebuildTrees(string definePath, string? revealFilePath = null)
    {
        if (string.IsNullOrEmpty(definePath))
        {
            Nodes = new ObservableCollection<DefineNode>();
            Solution = SolutionContext.Empty;
            ProgramNodes = new ObservableCollection<DefineNode>();
            TableSchemaNodes = new ObservableCollection<DefineNode>();
            return;
        }
        var root = DefinePathScanner.Scan(definePath);
        if (IsShownSolution(definePath)) TreeExpansion.Restore([root], TreeExpansion.Capture(Nodes));
        if (revealFilePath is not null) TreeExpansion.Reveal([root], revealFilePath);
        Nodes = new ObservableCollection<DefineNode> { root };
        Solution = SolutionContext.FromTree(root) with { DefinePath = definePath };
        RebuildContentTrees(definePath);
    }

    /// <summary>Rebuilds the views that are built from file contents rather than from the folder layout.</summary>
    private void RebuildContentTrees(string definePath)
    {
        var keep = IsShownSolution(definePath);
        var programs = ProgramTreeBuilder.Build(definePath).ToList();
        if (keep) TreeExpansion.Restore(programs, TreeExpansion.Capture(ProgramNodes));
        var databases = DbCategoryTreeBuilder.Build(definePath).ToList();
        if (keep) TreeExpansion.Restore(databases, TreeExpansion.Capture(TableSchemaNodes));
        ProgramNodes = new ObservableCollection<DefineNode>(programs);
        TableSchemaNodes = new ObservableCollection<DefineNode>(databases);
    }

    /// <summary>True when <paramref name="definePath"/> is the solution already shown, so its tree state carries over.</summary>
    private bool IsShownSolution(string definePath)
        => string.Equals(SolutionPath, definePath, StringComparison.OrdinalIgnoreCase);

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
            if (doc is FormSchemaDocumentViewModel form) form.ActionRequested -= OnFormSchemaActionRequested;
        }
        _dirtySubscriptions.Clear();
        foreach (var doc in OpenDocuments)
        {
            doc.PropertyChanged += OnDocumentPropertyChanged;
            doc.DefineFileGenerated += OnDefineFileGenerated;
            if (doc is FormSchemaDocumentViewModel form) form.ActionRequested += OnFormSchemaActionRequested;
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

        RebuildTrees(SolutionPath, revealFilePath: filePath);

        var node = FindNodeByFilePath(Nodes[0], filePath);
        if (node is not null) SelectedNode = node;
    }

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

        // A saved ProgramSettings, DbCategorySettings or FormSchema can change what the Programs and Table Schemas views
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

        var doc = new WelcomeDocumentViewModel(this);
        OpenDocuments.Insert(0, doc);
        ActiveDocument = doc;
    }

    /// <summary>Drops <paramref name="path"/> from the recent solutions (the Welcome page's × button).</summary>
    public void RemoveRecentSolution(string path)
    {
        var settings = UserSettings.Load();
        settings.RemoveRecentSolution(path);
        settings.Save();
        RecentSolutionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Empties the recent solutions (File → Open Recent → Clear Recent).</summary>
    public void ClearRecentSolutions()
    {
        var settings = UserSettings.Load();
        settings.RecentSolutions.Clear();
        settings.Save();
        RecentSolutionsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedNodeChanged(DefineNode? value) => OpenNode(value);

    partial void OnSelectedProgramNodeChanged(DefineNode? value) => OpenNode(value);

    partial void OnSelectedTableSchemaNodeChanged(DefineNode? value) => OpenNode(value);

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
            SelectedTableSchemaNode = null;

            var settings = UserSettings.Load();
            settings.TouchRecentSolution(definePath);
            settings.Save();
            RecentSolutionsChanged?.Invoke(this, EventArgs.Empty);

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
            SelectedTableSchemaNode = null;
            SolutionPath = string.Empty;
            StatusText = L("Status_OpenSolutionFailed", ex.Message);
        }
    }

    /// <summary>
    /// Opens the DefinePath named on the command line, or reports in the status bar that it is not a folder. The app
    /// still starts either way, so a project task that passed a wrong path shows why instead of exiting.
    /// </summary>
    public void OpenSolutionFromCommandLine(string definePath)
    {
        if (Directory.Exists(definePath)) OpenSolution(definePath);
        else StatusText = L("Status_StartupPathNotFound", definePath);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822",
        Justification = "RelayCommand handler bound from XAML must remain instance.")]
    [RelayCommand]
    private void ToggleTheme() => App.ToggleTheme();
}
