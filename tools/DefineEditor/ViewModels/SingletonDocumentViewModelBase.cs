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
