using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Carries the expanded state of the sidebar trees across a rebuild. Rescanning the solution creates every node
/// again, so without this the tree folds up each time a file is added or imported.
/// </summary>
public static class TreeExpansion
{
    /// <summary>The paths of the expanded nodes under <paramref name="roots"/>, as node names joined by <c>/</c>.</summary>
    public static HashSet<string> Capture(IEnumerable<DefineNode> roots)
    {
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots) Capture(root, string.Empty, expanded);
        return expanded;
    }

    /// <summary>Expands the nodes under <paramref name="roots"/> whose path is in <paramref name="expanded"/>.</summary>
    public static void Restore(IEnumerable<DefineNode> roots, IReadOnlySet<string> expanded)
    {
        foreach (var root in roots) Restore(root, string.Empty, expanded);
    }

    /// <summary>
    /// Expands every ancestor of the node of <paramref name="filePath"/>, so selecting it shows it.
    /// </summary>
    /// <returns><see langword="true"/> when the file was found.</returns>
    public static bool Reveal(IEnumerable<DefineNode> roots, string filePath)
        => roots.Any(root => Reveal(root, filePath));

    private static void Capture(DefineNode node, string parentPath, HashSet<string> expanded)
    {
        var path = parentPath + "/" + node.Name;
        if (node.IsExpanded) expanded.Add(path);
        foreach (var child in node.Children) Capture(child, path, expanded);
    }

    private static void Restore(DefineNode node, string parentPath, IReadOnlySet<string> expanded)
    {
        var path = parentPath + "/" + node.Name;
        if (expanded.Contains(path)) node.IsExpanded = true;
        foreach (var child in node.Children) Restore(child, path, expanded);
    }

    private static bool Reveal(DefineNode node, string filePath)
    {
        if (node.Kind == DefineNodeKind.DefineFile
            && string.Equals(node.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        foreach (var child in node.Children)
        {
            if (!Reveal(child, filePath)) continue;
            node.IsExpanded = true;
            return true;
        }
        return false;
    }
}
