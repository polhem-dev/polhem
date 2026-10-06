using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Tree editors whose nodes are the hand-built <see cref="SettingsTreeNode"/>, dispatched by
/// <see cref="SettingsTreeNode.Kind"/>. Editors move to the framework's <c>ObjectTreeNode</c> one at a time
/// (FormLayout first), after which this base goes away.
/// </summary>
public abstract class SingletonDocumentViewModelBase : TreeDocumentViewModelBase<SettingsTreeNode>
{
    protected SingletonDocumentViewModelBase(string filePath, string titlePrefix, string keyText)
        : base(filePath, titlePrefix, keyText)
    {
    }

    /// <summary>
    /// Whether the currently selected node can be deleted — visibility hint
    /// for the context-menu "Delete" item. Mirrors <c>CanDelete()</c> so the
    /// menu item appears exactly when the command would execute.
    /// </summary>
    public bool SelectedKindCanDelete =>
        SelectedTreeNode is not null && GetDeleteAction(SelectedTreeNode) is not null;

    /// <summary>
    /// Whether the context-menu separator between the Add items and the
    /// Delete item should show. A separator only makes sense when both
    /// sides are visible — a delete-only menu would otherwise render an
    /// orphaned line above its single item.
    /// </summary>
    public bool ShowDeleteSeparator => SelectedKindCanDelete && HasVisibleAddMenuItems;

    /// <summary>
    /// Whether any Add menu item is visible for the current selection —
    /// the OR of the subclass's <c>SelectedKindIsXxx</c> flags that gate
    /// its Add items. Drives <see cref="ShowDeleteSeparator"/>.
    /// </summary>
    protected abstract bool HasVisibleAddMenuItems { get; }

    protected override void OnSelectionMenuStateChanged()
    {
        OnPropertyChanged(nameof(SelectedKindCanDelete));
        OnPropertyChanged(nameof(ShowDeleteSeparator));
    }

    protected override object? GetNodeValue(SettingsTreeNode node) => node.Payload;

    protected override string GetNodeLabel(SettingsTreeNode node) => node.Header;

    protected override SettingsTreeNode? GetParentNode(SettingsTreeNode node) => node.Parent;

    protected override void RemoveNode(SettingsTreeNode node) => node.RemoveSelf();

    protected override void RefreshNodeLabels(SettingsTreeNode node) => node.RefreshRecursive();

    /// <summary>
    /// Walks up from <paramref name="node"/> until a node of <paramref name="kind"/> is found.
    /// </summary>
    protected static SettingsTreeNode? FindAncestor(SettingsTreeNode? node, string kind)
    {
        for (var cur = node; cur != null; cur = cur.Parent)
            if (cur.Kind == kind) return cur;
        return null;
    }
}
