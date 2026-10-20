using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.DefineEditor.Models;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Builds the Databases view of the sidebar: one node per <see cref="DbCategory"/> in <c>DbCategorySettings.xml</c>,
/// holding the tables registered in it. A table node opens <c>TableSchema/{category}/{table}.TableSchema.xml</c>.
/// </summary>
/// <remarks>
/// A registered table without a TableSchema file is not listed, and a TableSchema file that no category registers
/// does not appear; the Explorer view shows the files as they are.
/// </remarks>
public static class DbCategoryTreeBuilder
{
    private const string IconCategory = "DefCategory";
    private const string IconTableSchema = "DefTableSchema";

    /// <summary>
    /// Returns the category nodes of <paramref name="definePath"/> in the order DbCategorySettings declares them;
    /// empty when <c>DbCategorySettings.xml</c> is missing or cannot be read.
    /// </summary>
    public static IReadOnlyList<DefineNode> Build(string definePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definePath);

        var settings = DefineFileReader.TryRead<DbCategorySettings>(Path.Combine(definePath, "DbCategorySettings.xml"));
        if (settings?.Categories is not { } categories) return [];

        var nodes = new List<DefineNode>();
        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category.Id)) continue;
            var node = new DefineNode
            {
                Name = DefineFileReader.Label(category.Id, category.DisplayName),
                Kind = DefineNodeKind.Group,
                KeyText = category.Id,
                Icon = IconCategory,
            };
            foreach (var table in category.Tables ?? [])
            {
                if (string.IsNullOrWhiteSpace(table.TableName)) continue;
                var path = Path.Combine(definePath, "TableSchema", category.Id, $"{table.TableName}.TableSchema.xml");
                if (!File.Exists(path)) continue;
                node.Children.Add(new DefineNode
                {
                    Name = DefineFileReader.Label(table.TableName, table.DisplayName),
                    Kind = DefineNodeKind.DefineFile,
                    DefineType = DefineType.TableSchema,
                    FilePath = path,
                    KeyText = $"{category.Id}/{table.TableName}",
                    Icon = IconTableSchema,
                });
            }
            nodes.Add(node);
        }
        return nodes;
    }
}
