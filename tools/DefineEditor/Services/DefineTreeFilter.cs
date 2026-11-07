using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.Services;

/// <summary>Filters the sidebar trees and ranks files for Go to File.</summary>
public static class DefineTreeFilter
{
    /// <summary>
    /// A copy of the trees under <paramref name="roots"/> that keeps the nodes whose name or key contains
    /// <paramref name="text"/>, with everything under them, and the ancestors that lead to them, expanded.
    /// </summary>
    /// <remarks>
    /// The original nodes are left alone, so clearing the filter shows the trees with their own expanded state. Text
    /// compares ignoring case, the way definition keys do.
    /// </remarks>
    public static IReadOnlyList<DefineNode> Filter(IEnumerable<DefineNode> roots, string text)
    {
        var result = new List<DefineNode>();
        foreach (var root in roots)
        {
            if (FilterNode(root, text.Trim()) is { } kept)
                result.Add(kept);
        }
        return result;
    }

    private static DefineNode? FilterNode(DefineNode node, string text)
    {
        if (Matches(node, text))
            return Copy(node, expanded: node.IsExpanded, children: node.Children.Select(CopyAll));

        var children = node.Children.Select(c => FilterNode(c, text)).OfType<DefineNode>().ToList();
        return children.Count > 0 ? Copy(node, expanded: true, children) : null;
    }

    private static bool Matches(DefineNode node, string text) =>
        node.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
        || (node.KeyText?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);

    private static DefineNode CopyAll(DefineNode node) =>
        Copy(node, node.IsExpanded, node.Children.Select(CopyAll));

    private static DefineNode Copy(DefineNode node, bool expanded, IEnumerable<DefineNode> children)
    {
        var copy = new DefineNode
        {
            Name = node.Name,
            Kind = node.Kind,
            DefineType = node.DefineType,
            FilePath = node.FilePath,
            KeyText = node.KeyText,
            Icon = node.Icon,
            IsExpanded = expanded,
        };
        foreach (var child in children) copy.Children.Add(child);
        return copy;
    }

    /// <summary>Every definition file under <paramref name="roots"/>, in tree order.</summary>
    public static IEnumerable<DefineNode> Files(IEnumerable<DefineNode> roots)
    {
        foreach (var root in roots)
        {
            if (root.Kind == DefineNodeKind.DefineFile && !string.IsNullOrEmpty(root.FilePath))
                yield return root;
            foreach (var file in Files(root.Children))
                yield return file;
        }
    }

    /// <summary>
    /// How well <paramref name="query"/> matches <paramref name="candidate"/>, higher is better, or <c>null</c> when
    /// the query's characters do not all appear in the candidate in order.
    /// </summary>
    /// <remarks>
    /// The characters may be apart (<c>ordfs</c> finds <c>Order.FormSchema.xml</c>), so a few letters are enough. A
    /// run of adjacent characters, a match at the start of a word and a match at the start of the candidate count
    /// more, so a name that contains the query as typed comes before one that only scatters it.
    /// </remarks>
    public static int? Score(string candidate, string query)
    {
        if (query.Length == 0) return 0;
        var score = 0;
        var run = 0;
        var position = 0;
        foreach (var wanted in query)
        {
            var found = IndexOfIgnoreCase(candidate, wanted, position);
            if (found < 0) return null;
            run = found == position && position > 0 ? run + 1 : 0;
            score += 1 + run * 3;
            if (found == 0) score += 6;
            else if (IsWordStart(candidate, found)) score += 4;
            position = found + 1;
        }
        // Between equal matches, the shorter name is the closer one.
        return score * 100 - candidate.Length;
    }

    private static int IndexOfIgnoreCase(string text, char value, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (char.ToUpperInvariant(text[i]) == char.ToUpperInvariant(value)) return i;
        }
        return -1;
    }

    private static bool IsWordStart(string text, int index) =>
        !char.IsLetterOrDigit(text[index - 1]) || (char.IsUpper(text[index]) && char.IsLower(text[index - 1]));
}
