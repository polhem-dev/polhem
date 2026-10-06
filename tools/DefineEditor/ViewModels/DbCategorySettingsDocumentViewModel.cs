using Polhem.Core.Serialization;
using Polhem.Definition.Settings;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="DbCategorySettings"/>. Two-level tree:
/// DbCategorySettings → DbCategory[] → TableItem[]. Validation: duplicate /
/// empty category Ids, duplicate / empty table names within a category.
/// </summary>
public sealed partial class DbCategorySettingsDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public DbCategorySettings Root { get; }

    protected override object RootObject => Root;

    public override string TabIcon => "DefDbCategorySettings";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    private DbCategorySettingsDocumentViewModel(string filePath, DbCategorySettings root)
        : base(filePath, "DbCategorySettings", keyText: string.Empty, new ObjectTreeOptions { ExpandDepth = 1 })
    {
        Root = root;
        CommandProvider = new DbCategorySettingsCommandProvider(this);
        InitializeTree(root);
    }

    public static DbCategorySettingsDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("DbCategorySettings file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<DbCategorySettings>(filePath)
            ?? throw new InvalidOperationException($"DbCategorySettings deserialized to null: {filePath}");
        return new DbCategorySettingsDocumentViewModel(filePath, root);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        DbCategorySettings => "DefDbCategorySettings",
        DbCategory => "DefCategory",
        TableItem => "IconTable",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddCategory))]
    private void AddCategory()
    {
        if (SelectedTreeNode is not { Value: DbCategorySettings root } rootNode)
            return;
        var id = UniqueKey(root.Categories!.Select(c => c.Id), "new_category");
        var category = new DbCategory { Id = id, DisplayName = "New category" };
        root.Categories!.Add(category);
        AddNode(rootNode, category);
        StatusText = L("Status_AddedNamed", "DbCategory", id);
    }

    private bool CanAddCategory() => SelectedTreeNode?.Value is DbCategorySettings;

    [RelayCommand(CanExecute = nameof(CanAddTable))]
    private void AddTable()
    {
        var categoryNode = FindAncestor<DbCategory>(SelectedTreeNode);
        if (categoryNode?.Value is not DbCategory category) return;
        var name = UniqueKey(category.Tables!.Select(t => t.TableName), "new_table");
        var table = new TableItem { TableName = name, DisplayName = "New table" };
        category.Tables!.Add(table);
        AddNode(categoryNode, table);
        StatusText = L("Status_AddedNamed", "TableItem", name);
    }

    private bool CanAddTable() => FindAncestor<DbCategory>(SelectedTreeNode) is not null;

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        DbCategory c when node.Parent?.Value is DbCategorySettings p => () => p.Categories!.Remove(c),
        TableItem t when node.Parent?.Value is DbCategory pc => () => pc.Tables!.Remove(t),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
    {
        var issues = new List<ValidationIssue>();
        var categories = Root.Categories;
        if (categories is null || categories.Count == 0)
        {
            issues.Add(new(ValidationSeverity.Warning, "DbCategorySettings", "No DbCategory has been defined."));
            return issues;
        }
        var seenCategoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories)
        {
            var catPath = !string.IsNullOrWhiteSpace(category.Id) ? category.Id : "(unnamed)";
            if (string.IsNullOrWhiteSpace(category.Id))
                issues.Add(new(ValidationSeverity.Error, catPath, "DbCategory.Id cannot be empty."));
            else if (!seenCategoryIds.Add(category.Id))
                issues.Add(new(ValidationSeverity.Error, catPath,
                    $"DbCategory.Id '{category.Id}' is a duplicate."));

            var seenTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in category.Tables ?? Enumerable.Empty<TableItem>())
            {
                var path = $"{catPath}.{(string.IsNullOrEmpty(table.TableName) ? "(unnamed)" : table.TableName)}";
                if (string.IsNullOrWhiteSpace(table.TableName))
                    issues.Add(new(ValidationSeverity.Error, path, "TableItem.TableName cannot be empty."));
                else if (!seenTableNames.Add(table.TableName))
                    issues.Add(new(ValidationSeverity.Error, path,
                        $"TableItem.TableName '{table.TableName}' is a duplicate within '{catPath}'."));
            }
        }
        return issues;
    }
}
