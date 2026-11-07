using CommunityToolkit.Mvvm.Input;
using Polhem.Core.Serialization;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

// Undo and redo. Every edit of the define object ends in MarkChanged, so the history keeps a snapshot of the whole
// object there, serialized with XmlCodec like a save, instead of a reverse command for each kind of edit. Undo puts an
// earlier snapshot back and rebuilds the tree from it.
public abstract partial class ObjectTreeDocumentViewModelBase
{
    // A definition file is small, so a hundred snapshots of it are cheap.
    private const int HistoryLimit = 100;

    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    // The history entry that matches the file on disk; -1 once that entry has been dropped.
    private int _savedIndex = -1;

    private bool _isRestoring;

    /// <summary>Replaces the define object the document edits with <paramref name="root"/>, of the same type.</summary>
    protected abstract void ReplaceRoot(object root);

    /// <summary>Whether there is an edit to undo.</summary>
    public bool CanUndo => _historyIndex > 0;

    /// <summary>Whether there is an undone edit to redo.</summary>
    public bool CanRedo => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    /// <summary>The Undo button's tooltip, with its key.</summary>
    public string UndoToolTip => $"{L("Action_Undo")} ({TreeShortcuts.Undo})";

    /// <summary>The Redo button's tooltip, with its key.</summary>
    public string RedoToolTip => $"{L("Action_Redo")} ({TreeShortcuts.Redo})";

    /// <summary>Puts back the define object as it was before the last edit.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        Restore(_historyIndex - 1);
        StatusText = L("Status_Undone");
    }

    /// <summary>Applies again the edit the last Undo took back.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        Restore(_historyIndex + 1);
        StatusText = L("Status_Redone");
    }

    private void StartHistory(object root)
    {
        _history.Clear();
        _history.Add(XmlCodec.Serialize(root));
        _historyIndex = 0;
        _savedIndex = 0;
        NotifyHistory();
    }

    // Records the define object after an edit, dropping what was undone before it. An edit that left the object as
    // it was (an add the grid could not apply, say) records nothing.
    private void RecordHistory()
    {
        if (_isRestoring || _historyIndex < 0) return;

        var snapshot = XmlCodec.Serialize(RootObject);
        if (snapshot == _history[_historyIndex]) return;

        _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        if (_savedIndex > _historyIndex) _savedIndex = -1;
        _history.Add(snapshot);
        _historyIndex++;
        if (_history.Count > HistoryLimit)
        {
            _history.RemoveAt(0);
            _historyIndex--;
            _savedIndex = _savedIndex > 0 ? _savedIndex - 1 : -1;
        }
        NotifyHistory();
    }

    // The document is unchanged exactly when the history stands on the entry that was saved.
    private bool IsAtSavedState => _historyIndex >= 0 && _historyIndex == _savedIndex;

    private void MarkSaved()
    {
        _savedIndex = _historyIndex;
    }

    private void Restore(int index)
    {
        if (index < 0 || index >= _history.Count) return;

        var selectedPath = PathOf(SelectedTreeNode);
        var selectedLabel = SelectedTreeNode?.Label;
        var expanded = Roots.SelectMany(r => ExpandedPaths(r, string.Empty)).ToHashSet(StringComparer.Ordinal);
        var root = XmlCodec.Deserialize(_history[index], RootObject.GetType())
            ?? throw new InvalidOperationException($"{RootObject.GetType().Name} could not be restored.");

        _historyIndex = index;
        _isRestoring = true;
        try
        {
            ReplaceRoot(root);
            Roots.Clear();
            Roots.Add(Builder.Build(root));
            ApplyExpanded(Roots[0], string.Empty, expanded);
            OnPropertyChanged(nameof(RootNode));
            SelectedTreeNode = FindByLabel(selectedLabel, selectedPath) ?? NodeAt(selectedPath);
            IsDirty = !IsAtSavedState;
            RefreshToolbar();
            EditorRefreshRequested?.Invoke(this, EventArgs.Empty);
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isRestoring = false;
        }
        NotifyHistory();
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // The node that shows the same object as before, found by its label; a move changes its place but not its label.
    // Labels can repeat (a form and its master table are often both "Order - Order"), so among equal labels the one
    // nearest the old place wins. An undone rename changes the label, and the place is used instead.
    private ObjectTreeNode? FindByLabel(string? label, List<int> path) =>
        label is null
            ? null
            : Roots.SelectMany(Enumerate).Where(n => n.Label == label)
                .OrderByDescending(n => CommonPrefix(PathOf(n), path))
                .FirstOrDefault();

    private static int CommonPrefix(List<int> a, List<int> b)
    {
        var length = 0;
        while (length < a.Count && length < b.Count && a[length] == b[length]) length++;
        // An exact place beats a deeper node that only shares the start of it.
        return length * 2 + (a.Count == b.Count ? 1 : 0);
    }

    // A node's place in the tree as child indexes from the root, which survives a rebuild of the same structure.
    private static List<int> PathOf(ObjectTreeNode? node)
    {
        var path = new List<int>();
        for (var current = node; current?.Parent is { } parent; current = parent)
            path.Insert(0, parent.Children.IndexOf(current));
        return path;
    }

    // The node at path, or the deepest node on the way when the restored tree is shorter there.
    private ObjectTreeNode? NodeAt(List<int> path)
    {
        if (Roots.Count == 0) return null;
        var node = Roots[0];
        foreach (var index in path)
        {
            if (index < 0 || index >= node.Children.Count) break;
            node = node.Children[index];
        }
        return node;
    }

    private static IEnumerable<string> ExpandedPaths(ObjectTreeNode node, string path)
    {
        if (node.IsExpanded) yield return path;
        for (var i = 0; i < node.Children.Count; i++)
        {
            foreach (var child in ExpandedPaths(node.Children[i], $"{path}/{i}"))
                yield return child;
        }
    }

    private static void ApplyExpanded(ObjectTreeNode node, string path, HashSet<string> expanded)
    {
        node.IsExpanded = expanded.Contains(path);
        for (var i = 0; i < node.Children.Count; i++)
            ApplyExpanded(node.Children[i], $"{path}/{i}", expanded);
    }
}
