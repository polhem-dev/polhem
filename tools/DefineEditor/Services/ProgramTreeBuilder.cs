using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Builds the Programs view of the sidebar: one node per <see cref="ProgramItem"/> in <c>ProgramSettings.xml</c>,
/// holding the define files of that ProgId found by the file-name conventions of the DefinePath.
/// </summary>
/// <remarks>
/// Only files that exist are listed, and a ProgId that has a FormSchema but is not registered in ProgramSettings
/// does not appear; the Explorer view shows both.
/// </remarks>
public static class ProgramTreeBuilder
{
    private const string IconProgram = "DefProgramSettings";
    private const string IconFormSchema = "DefFormSchema";
    private const string IconFormLayout = "DefFormLayout";
    private const string IconTableSchema = "DefTableSchema";
    private const string IconLanguage = "DefLanguage";

    /// <summary>
    /// Returns the program nodes of <paramref name="definePath"/> in the order ProgramSettings declares them;
    /// empty when <c>ProgramSettings.xml</c> is missing or cannot be read.
    /// </summary>
    public static IReadOnlyList<DefineNode> Build(string definePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definePath);

        var settings = DefineFileReader.TryRead<ProgramSettings>(Path.Combine(definePath, "ProgramSettings.xml"));
        if (settings?.Items is not { } items) return [];

        var nodes = new List<DefineNode>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProgId)) continue;
            var node = new DefineNode
            {
                Name = DefineFileReader.Label(item.ProgId, item.DisplayName),
                Kind = DefineNodeKind.Group,
                KeyText = item.ProgId,
                Icon = IconProgram,
            };
            AddChildren(node, definePath, item.ProgId);
            nodes.Add(node);
        }
        return nodes;
    }

    private static void AddChildren(DefineNode node, string definePath, string progId)
    {
        var formSchemaPath = Path.Combine(definePath, "FormSchema", $"{progId}.FormSchema.xml");
        if (File.Exists(formSchemaPath))
        {
            node.Children.Add(FileNode("FormSchema", DefineType.FormSchema, formSchemaPath, progId, IconFormSchema));
        }

        var formLayoutPath = Path.Combine(definePath, "FormLayout", $"{progId}.FormLayout.xml");
        if (File.Exists(formLayoutPath))
        {
            node.Children.Add(FileNode("FormLayout", DefineType.FormLayout, formLayoutPath, progId, IconFormLayout));
        }

        foreach (var (key, path) in TableSchemaFiles(definePath, formSchemaPath))
        {
            node.Children.Add(FileNode(Path.GetFileName(key), DefineType.TableSchema, path, key, IconTableSchema));
        }

        var languageDir = Path.Combine(definePath, "Language");
        if (!Directory.Exists(languageDir)) return;
        foreach (var cultureDir in Directory.EnumerateDirectories(languageDir).Order(StringComparer.OrdinalIgnoreCase))
        {
            var culture = Path.GetFileName(cultureDir);
            var path = Path.Combine(cultureDir, $"{progId}.Language.xml");
            if (File.Exists(path))
            {
                node.Children.Add(FileNode($"Language ({culture})", DefineType.Language, path, $"{culture}/{progId}",
                    IconLanguage));
            }
        }
    }

    // The tables of the FormSchema (master first, then the details, as the schema declares them), located the way
    // the framework does: TableSchema/{CategoryId}/{DbTableName}, where an empty DbTableName means the TableName.
    private static IEnumerable<(string Key, string Path)> TableSchemaFiles(string definePath, string formSchemaPath)
    {
        if (!File.Exists(formSchemaPath)) yield break;
        var schema = DefineFileReader.TryRead<FormSchema>(formSchemaPath);
        if (schema?.Tables is not { } tables || string.IsNullOrWhiteSpace(schema.CategoryId)) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            var dbTableName = string.IsNullOrWhiteSpace(table.DbTableName) ? table.TableName : table.DbTableName;
            if (string.IsNullOrWhiteSpace(dbTableName)) continue;
            var path = Path.Combine(definePath, "TableSchema", schema.CategoryId, $"{dbTableName}.TableSchema.xml");
            if (File.Exists(path) && seen.Add(path))
                yield return ($"{schema.CategoryId}/{dbTableName}", path);
        }
    }

    private static DefineNode FileNode(string name, DefineType type, string path, string key, string icon) => new()
    {
        Name = name,
        Kind = DefineNodeKind.DefineFile,
        DefineType = type,
        FilePath = path,
        KeyText = key,
        Icon = icon,
    };
}
