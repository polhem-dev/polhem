using System.Xml;
using System.Xml.Linq;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The steps of syncing a FormSchema to its derived files and of upgrading its tables that need no window: what each
/// planned file gains, and which of the form's tables the database manager would not see.
/// </summary>
public static class FormSchemaSync
{
    /// <summary>
    /// One bullet line per file <paramref name="plan"/> writes: its path, what happens to it, and the fields and tables
    /// an existing file gains.
    /// </summary>
    /// <param name="plan">A plan from <see cref="FormSync.Plan"/>.</param>
    /// <param name="definePath">The definition folder the paths are shown relative to.</param>
    public static string DescribeWrites(FormImportPlan plan, string definePath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return string.Join("\n", plan.Files.Where(f => f.Writes).Select(f =>
        {
            var line = $"• {Path.GetRelativePath(definePath, f.Path)} ({FormWorkbookImport.ActionLabel(f.Action)})";
            var added = File.Exists(f.Path) ? AddedNames(File.ReadAllText(f.Path), f.Content!) : [];
            return added.Count == 0 ? line : $"{line}: + {string.Join(", ", added)}";
        }));
    }

    /// <summary>
    /// The tables and fields named in <paramref name="updated"/> that <paramref name="existing"/> does not name, in
    /// document order.
    /// </summary>
    public static IReadOnlyList<string> AddedNames(string existing, string updated)
    {
        var before = Names(existing).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Names(updated).Where(before.Add).ToList();
    }

    /// <summary>
    /// The tables of <paramref name="schema"/> that the DbCategorySettings of <paramref name="definePath"/> does not
    /// register. The database manager upgrades only registered tables, so these need a sync first.
    /// </summary>
    public static IReadOnlyList<string> UnregisteredTables(string definePath, FormSchema schema)
    {
        var path = new PathOptions { DefinePath = Path.GetFullPath(definePath) }.GetDbCategorySettingsFilePath();
        var registered = File.Exists(path)
            ? Load(File.ReadAllText(path)).Descendants("TableItem")
                .Select(e => (string?)e.Attribute("TableName") ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        return FormSync.TableNames(schema).Where(t => !registered.Contains(t)).ToList();
    }

    /// <summary>
    /// The names a definition file holds: each <c>TableName</c>, and each <c>FieldName</c> qualified with the
    /// <c>TableName</c> of the element it sits in below the root, so a detail column is told apart from a master
    /// field of the same name.
    /// </summary>
    private static IEnumerable<string> Names(string xml)
    {
        var root = Load(xml).Root!;
        foreach (var element in root.Descendants())
        {
            if ((string?)element.Attribute("TableName") is { Length: > 0 } table) yield return table;
            if ((string?)element.Attribute("FieldName") is not { Length: > 0 } field) continue;
            var owner = element.Ancestors().TakeWhile(a => a != root)
                .Select(a => (string?)a.Attribute("TableName")).FirstOrDefault(t => !string.IsNullOrEmpty(t));
            yield return owner is null ? field : $"{owner}.{field}";
        }
    }

    private static XDocument Load(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        return XDocument.Load(reader);
    }
}
