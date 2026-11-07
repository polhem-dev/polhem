using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

// Find in tree: selects the nodes whose label contains the text, one at a time, expanding their ancestors. The tree is
// not filtered, so the matches stay among the nodes around them.
public abstract partial class ObjectTreeDocumentViewModelBase
{
    /// <summary>The placeholder of the Find in tree box, with its key, so the box says what it is and how to reach it.</summary>
    public string TreeFindWatermark => $"{L("TreeFind_Watermark")} ({(OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F")})";

    /// <summary>The text Find in tree looks for in the node labels, ignoring case.</summary>
    [ObservableProperty]
    private string _treeFindText = string.Empty;

    /// <summary>Where the selection is among the matches (<c>2 of 5</c>), or that there are none; empty with no text.</summary>
    [ObservableProperty]
    private string _treeFindStatus = string.Empty;

    // Typing keeps the selection when it still matches, so the next character narrows instead of jumping away.
    partial void OnTreeFindTextChanged(string value) => Find(0);

    /// <summary>Selects the next match after the selected node, wrapping to the first.</summary>
    [RelayCommand]
    private void FindNext() => Find(1);

    /// <summary>Selects the match before the selected node, wrapping to the last.</summary>
    [RelayCommand]
    private void FindPrevious() => Find(-1);

    private void Find(int step)
    {
        var text = TreeFindText.Trim();
        if (text.Length == 0)
        {
            TreeFindStatus = string.Empty;
            return;
        }

        var nodes = Roots.SelectMany(Enumerate).ToList();
        var matches = nodes.Where(n => n.Label.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            TreeFindStatus = L("TreeFind_NoMatches");
            return;
        }

        var target = PickMatch(nodes, matches, step);
        for (var ancestor = target.Parent; ancestor != null; ancestor = ancestor.Parent)
            ancestor.IsExpanded = true;
        SelectedTreeNode = target;
        TreeFindStatus = L("TreeFind_Position", matches.IndexOf(target) + 1, matches.Count);
    }

    // With step 0 the selection stays when it matches; otherwise the search starts from the selection in tree order,
    // forwards or backwards, and wraps around.
    private ObjectTreeNode PickMatch(List<ObjectTreeNode> nodes, List<ObjectTreeNode> matches, int step)
    {
        var current = SelectedTreeNode is { } selected ? matches.IndexOf(selected) : -1;
        if (current >= 0)
            return step == 0 ? matches[current] : matches[(current + step + matches.Count) % matches.Count];

        var position = SelectedTreeNode is { } node ? nodes.IndexOf(node) : -1;
        return step < 0
            ? matches.LastOrDefault(m => nodes.IndexOf(m) < position) ?? matches[^1]
            : matches.FirstOrDefault(m => nodes.IndexOf(m) > position) ?? matches[0];
    }

    private static IEnumerable<ObjectTreeNode> Enumerate(ObjectTreeNode node) =>
        new[] { node }.Concat(node.Children.SelectMany(Enumerate));
}
