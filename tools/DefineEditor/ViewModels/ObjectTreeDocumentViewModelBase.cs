using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Tree editors whose tree the framework's <see cref="ObjectTreeBuilder"/> builds from the
/// <c>[TreeNode]</c> annotations and whose context menu comes from an
/// <see cref="ITreeNodeCommandProvider"/>. A subclass sets its root object, calls
/// <see cref="InitializeTree"/>, and supplies the icon of each node and its commands.
/// </summary>
public abstract class ObjectTreeDocumentViewModelBase : TreeDocumentViewModelBase<ObjectTreeNode>
{
    protected ObjectTreeDocumentViewModelBase(
        string filePath, string titlePrefix, string keyText, ObjectTreeOptions treeOptions)
        : base(filePath, titlePrefix, keyText)
    {
        Builder = new ObjectTreeBuilder(treeOptions);
    }

    /// <summary>Builds the tree and the nodes for objects added later, with the subclass's options.</summary>
    protected ObjectTreeBuilder Builder { get; }

    /// <summary>The root of the tree, which the tree view binds.</summary>
    public ObjectTreeNode RootNode => Roots[0];

    /// <summary>The commands of the tree's context menu.</summary>
    public abstract ITreeNodeCommandProvider CommandProvider { get; }

    /// <summary>The resource key of the icon shown before <paramref name="node"/>.</summary>
    public abstract string IconKeyFor(ObjectTreeNode node);

    /// <summary><see cref="IconKeyFor"/> as a delegate, for the tree view's icon selector binding.</summary>
    public Func<ObjectTreeNode, string> IconKeys => IconKeyFor;

    /// <summary>Builds the tree for <paramref name="root"/> and selects its root.</summary>
    protected void InitializeTree(object root)
    {
        Roots.Add(Builder.Build(root));
        SelectedTreeNode = Roots[0];
    }

    /// <summary>The first folder directly under the root whose collection is a <typeparamref name="TCollection"/>.</summary>
    protected ObjectTreeNode? FolderOf<TCollection>() =>
        RootNode.Children.FirstOrDefault(c => c.IsFolder && c.Value is TCollection);

    /// <summary>Walks up from <paramref name="node"/> to the first node that stands for a <typeparamref name="TValue"/>.</summary>
    protected static ObjectTreeNode? FindAncestor<TValue>(ObjectTreeNode? node)
    {
        for (var cur = node; cur != null; cur = cur.Parent)
            if (cur.Value is TValue) return cur;
        return null;
    }

    /// <summary>
    /// Builds a node for <paramref name="value"/>, which the caller has just added to the define
    /// object, attaches it under <paramref name="parent"/>, selects it and marks the document dirty.
    /// </summary>
    protected ObjectTreeNode AddNode(ObjectTreeNode parent, object value)
    {
        var node = Builder.Build(value);
        node.IsExpanded = false;
        parent.Children.Add(node);
        parent.IsExpanded = true;
        SelectedTreeNode = node;
        IsDirty = true;
        return node;
    }

    protected override object? GetNodeValue(ObjectTreeNode node) => node.IsFolder ? null : node.Value;

    protected override string GetNodeLabel(ObjectTreeNode node) => node.Label;

    protected override ObjectTreeNode? GetParentNode(ObjectTreeNode node) => node.Parent;

    protected override void RemoveNode(ObjectTreeNode node) => node.Parent?.Children.Remove(node);

    protected override void RefreshNodeLabels(ObjectTreeNode node)
    {
        node.Refresh();
        foreach (var child in node.Children)
            RefreshNodeLabels(child);
    }
}
