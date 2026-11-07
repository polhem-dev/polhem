using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

// The sidebar filter and Go to File.
public partial class MainWindowViewModel
{
    /// <summary>The text the sidebar views are filtered by; empty shows them whole.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleNodes), nameof(VisibleProgramNodes), nameof(VisibleTableSchemaNodes),
        nameof(IsSidebarFiltered), nameof(HasNoFilterMatches))]
    private string _sidebarFilter = string.Empty;

    /// <summary>Whether <see cref="SidebarFilter"/> holds text.</summary>
    public bool IsSidebarFiltered => !string.IsNullOrWhiteSpace(SidebarFilter);

    /// <summary>The Explorer view as the sidebar shows it: <see cref="Nodes"/>, filtered.</summary>
    public IReadOnlyList<DefineNode> VisibleNodes => Visible(Nodes);

    /// <summary>The Programs view as the sidebar shows it: <see cref="ProgramNodes"/>, filtered.</summary>
    public IReadOnlyList<DefineNode> VisibleProgramNodes => Visible(ProgramNodes);

    /// <summary>The Table Schemas view as the sidebar shows it: <see cref="TableSchemaNodes"/>, filtered.</summary>
    public IReadOnlyList<DefineNode> VisibleTableSchemaNodes => Visible(TableSchemaNodes);

    /// <summary>Whether the filter leaves nothing in the view the sidebar shows.</summary>
    public bool HasNoFilterMatches => IsSidebarFiltered && ActiveSidebarView switch
    {
        SidebarView.Programs => VisibleProgramNodes.Count == 0,
        SidebarView.TableSchemas => VisibleTableSchemaNodes.Count == 0,
        _ => VisibleNodes.Count == 0,
    };

    private IReadOnlyList<DefineNode> Visible(ObservableCollection<DefineNode> roots) =>
        IsSidebarFiltered ? DefineTreeFilter.Filter(roots, SidebarFilter) : roots;

    partial void OnNodesChanged(ObservableCollection<DefineNode> value)
    {
        OnPropertyChanged(nameof(VisibleNodes));
        OnPropertyChanged(nameof(HasNoFilterMatches));
    }

    [RelayCommand]
    private void ClearSidebarFilter() => SidebarFilter = string.Empty;

    /// <summary>The key of Go to File as the title bar's search field shows it.</summary>
    public string GoToFileShortcut => OperatingSystem.IsMacOS() ? "⌘P" : "Ctrl+P";

    /// <summary>The tooltip of the sidebar's Go to File button, with its key.</summary>
    public string GoToFileToolTip => $"{L("MenuItem_GoToFile")} ({GoToFileShortcut})";

    /// <summary>Whether the Go to File box is open.</summary>
    [ObservableProperty]
    private bool _isQuickOpenVisible;

    /// <summary>The text typed in the Go to File box.</summary>
    [ObservableProperty]
    private string _quickOpenText = string.Empty;

    /// <summary>
    /// The files Go to File lists for <see cref="QuickOpenText"/>, best match first: the solution's files that the
    /// text matches by name, key or path, with the characters allowed to be apart.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<QuickOpenItem> _quickOpenItems = [];

    /// <summary>The highlighted file in the Go to File list, which Enter opens.</summary>
    [ObservableProperty]
    private QuickOpenItem? _selectedQuickOpenItem;

    partial void OnQuickOpenTextChanged(string value) => RefreshQuickOpenItems();

    private void RefreshQuickOpenItems()
    {
        QuickOpenItems = DefineTreeFilter.Files(Nodes)
            .Select(node => new QuickOpenItem(node, RelativePath(node.FilePath!)))
            .Select(item => (item, score: Best(item, QuickOpenText.Trim())))
            .Where(x => x.score is not null)
            .OrderByDescending(x => x.score)
            .Take(QuickOpenLimit)
            .Select(x => x.item)
            .ToList();
        SelectedQuickOpenItem = QuickOpenItems.FirstOrDefault();
    }

    // Enough to pick from without scrolling; a longer query narrows it.
    private const int QuickOpenLimit = 50;

    // The name followed by the type ("Order FormLayout") lets a few letters of each pick one file of a form, so
    // "ordfl" finds Order's layout rather than any path that happens to contain those letters.
    private static int? Best(QuickOpenItem item, string query) =>
        new[]
        {
            DefineTreeFilter.Score(item.Node.Name, query),
            item.Node.DefineType is { } type ? DefineTreeFilter.Score($"{item.Node.Name} {type}", query) : null,
            item.Node.KeyText is { } key ? DefineTreeFilter.Score(key, query) : null,
            DefineTreeFilter.Score(item.Detail, query) is { } path ? path - 1000 : null,
        }.Max();

    private string RelativePath(string filePath) =>
        string.IsNullOrEmpty(SolutionPath) ? filePath : Path.GetRelativePath(SolutionPath, filePath);

    /// <summary>Opens the Go to File box, empty.</summary>
    [RelayCommand]
    private void ShowQuickOpen()
    {
        if (!IsSolutionOpened) return;
        QuickOpenText = string.Empty;
        RefreshQuickOpenItems();
        IsQuickOpenVisible = true;
    }

    /// <summary>Closes the Go to File box without opening anything.</summary>
    [RelayCommand]
    private void HideQuickOpen() => IsQuickOpenVisible = false;

    /// <summary>Opens <paramref name="item"/>, or the highlighted file, in a tab and closes the Go to File box.</summary>
    [RelayCommand]
    private void OpenQuickOpenItem(QuickOpenItem? item)
    {
        if ((item ?? SelectedQuickOpenItem) is not { } target) return;
        IsQuickOpenVisible = false;
        OpenNode(target.Node);
    }

    /// <summary>Moves the highlight in the Go to File list by <paramref name="step"/>, stopping at either end.</summary>
    public void MoveQuickOpenSelection(int step)
    {
        if (QuickOpenItems.Count == 0) return;
        var index = SelectedQuickOpenItem is { } selected ? IndexOf(QuickOpenItems, selected) : -1;
        SelectedQuickOpenItem = QuickOpenItems[Math.Clamp(index + step, 0, QuickOpenItems.Count - 1)];
    }

    private static int IndexOf(IReadOnlyList<QuickOpenItem> items, QuickOpenItem item)
    {
        for (var i = 0; i < items.Count; i++)
            if (ReferenceEquals(items[i], item)) return i;
        return -1;
    }
}
