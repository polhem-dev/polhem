using System.Collections;
using System.ComponentModel;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Lets a node be dragged before or after a sibling that lives in the same define collection, which
/// reorders that collection. Folders and objects outside a collection cannot be dragged, and nothing
/// moves to another parent.
/// </summary>
public sealed class SiblingReorderDragDropHandler(ObjectTreeDocumentViewModelBase document) : ITreeNodeDragDropHandler
{
    public bool CanDrag(ObjectTreeNode node) => !node.IsFolder && OwningList(node) is not null;

    public bool CanDrop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position) =>
        ReferenceEquals(node.Parent, target.Parent)
        && !target.IsFolder
        && OwningList(node) is { } list
        && IndexOf(list, target.Value) >= 0;

    public void Drop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position)
    {
        var list = OwningList(node)!;
        list.RemoveAt(IndexOf(list, node.Value));
        var index = IndexOf(list, target.Value) + (position == TreeNodeDropPosition.After ? 1 : 0);
        list.Insert(index, node.Value);
        document.IsDirty = true;
    }

    /// <summary>
    /// The define collection that holds the node's object: the folder's collection under a folder, or
    /// otherwise the collection property of the parent's object that contains it.
    /// </summary>
    private static IList? OwningList(ObjectTreeNode node)
    {
        if (node.Parent is not { } parent) return null;
        if (parent.IsFolder)
            return parent.Value is IList folderList && IndexOf(folderList, node.Value) >= 0 ? folderList : null;
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(parent.Value))
        {
            if (!typeof(IList).IsAssignableFrom(property.PropertyType)) continue;
            if (property.GetValue(parent.Value) is IList list && IndexOf(list, node.Value) >= 0) return list;
        }
        return null;
    }

    // Reference identity, because a define item may override Equals.
    private static int IndexOf(IList list, object value)
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], value)) return i;
        return -1;
    }
}
