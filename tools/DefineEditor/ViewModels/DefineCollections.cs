using System.Collections;
using System.ComponentModel;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>Finds the define collection a tree node's object lives in, for reordering and duplicating it.</summary>
public static class DefineCollections
{
    /// <summary>
    /// The define collection that holds the node's object: the folder's collection under a folder, or otherwise the
    /// collection property of the parent's object that contains it; <c>null</c> for a root or an object outside a
    /// collection.
    /// </summary>
    public static IList? OwningList(ObjectTreeNode node)
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

    /// <summary>The index of <paramref name="value"/> in <paramref name="list"/> by reference, or -1.</summary>
    /// <remarks>By reference, because a define item may override <see cref="object.Equals(object)"/>.</remarks>
    public static int IndexOf(IList list, object value)
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], value)) return i;
        return -1;
    }
}
