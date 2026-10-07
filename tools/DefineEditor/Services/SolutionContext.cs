using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// A snapshot of solution-wide information that individual document editors
/// need but cannot derive from a single file (currently: the set of FormSchema
/// ProgIds in the same DefinePath, used to drive RelationProgId / LookupProgId
/// dropdowns and "unknown ProgId" validation).
/// </summary>
public sealed record SolutionContext(IReadOnlyList<string> AvailableProgIds)
{
    public static SolutionContext Empty { get; } = new(Array.Empty<string>());

    /// <summary>The FormSchema file of each ProgId in <see cref="AvailableProgIds"/>, when the tree gave one.</summary>
    public IReadOnlyDictionary<string, string> FormSchemaPaths { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The field names of the master table of the FormSchema for <paramref name="progId"/>, read from its file each
    /// time; empty when the ProgId is unknown or the file cannot be read.
    /// </summary>
    public IReadOnlyList<string> MasterFieldNames(string progId)
    {
        if (string.IsNullOrEmpty(progId) || !FormSchemaPaths.TryGetValue(progId, out var path)) return [];
        try
        {
            var schema = XmlCodec.DeserializeFromFile<FormSchema>(path);
            return schema?.MasterTable?.Fields?.Select(f => f.FieldName).Where(n => !string.IsNullOrEmpty(n)).ToArray() ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A file that cannot be read offers no suggestions; validation reports the broken file elsewhere.
            return [];
        }
    }

    /// <summary>
    /// Walks the solution tree and collects all FormSchema ProgIds present.
    /// </summary>
    public static SolutionContext FromTree(DefineNode? root)
    {
        if (root is null) return Empty;
        var progIds = new List<string>();
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Walk(root, progIds, paths);
        progIds.Sort(StringComparer.OrdinalIgnoreCase);
        return new SolutionContext(progIds) { FormSchemaPaths = paths };
    }

    private static void Walk(DefineNode node, List<string> sink, Dictionary<string, string> paths)
    {
        if (node.Kind == DefineNodeKind.DefineFile
            && node.DefineType == DefineType.FormSchema
            && !string.IsNullOrEmpty(node.KeyText))
        {
            sink.Add(node.KeyText);
            if (!string.IsNullOrEmpty(node.FilePath))
                paths.TryAdd(node.KeyText, node.FilePath);
        }
        foreach (var child in node.Children)
            Walk(child, sink, paths);
    }
}
