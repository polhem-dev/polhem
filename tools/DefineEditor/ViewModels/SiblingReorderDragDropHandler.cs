using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Lets a node be dragged before or after a sibling that lives in the same define collection, which
/// reorders that collection. Folders and objects outside a collection cannot be dragged, and nothing
/// moves to another parent.
/// </summary>
/// <remarks>The tree's Move Up and Move Down commands reorder through <see cref="Drop"/> too.</remarks>
public sealed class SiblingReorderDragDropHandler(ObjectTreeDocumentViewModelBase document) : ITreeNodeDragDropHandler
{
    public bool CanDrag(ObjectTreeNode node) => !node.IsFolder && DefineCollections.OwningList(node) is not null;

    public bool CanDrop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position) =>
        ReferenceEquals(node.Parent, target.Parent)
        && !target.IsFolder
        && DefineCollections.OwningList(node) is { } list
        && DefineCollections.IndexOf(list, target.Value) >= 0;

    public void Drop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position)
    {
        var list = DefineCollections.OwningList(node)!;
        list.RemoveAt(DefineCollections.IndexOf(list, node.Value));
        var index = DefineCollections.IndexOf(list, target.Value) + (position == TreeNodeDropPosition.After ? 1 : 0);
        list.Insert(index, node.Value);
        document.MarkChanged();
    }
}
