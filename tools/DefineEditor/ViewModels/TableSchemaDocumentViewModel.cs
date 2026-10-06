using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Sorting;
using Polhem.DefineEditor.Models;
using Polhem.Definition.ObjectTree;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="TableSchema"/>. Tree: TableSchema → Fields group →
/// DbField[]; TableSchema → Indexes group → DbTableIndex[] → IndexField[].
/// </summary>
public sealed partial class TableSchemaDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public TableSchema Root { get; }
    protected override object RootObject => Root;

    public override string TabIcon => "DefTableSchema";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    private TableSchemaDocumentViewModel(string filePath, TableSchema root)
        // The root and the Fields and Indexes folders start expanded.
        : base(filePath, "TableSchema", keyText: root.TableName, new ObjectTreeOptions { ExpandDepth = 2 })
    {
        Root = root;
        CommandProvider = new TableSchemaCommandProvider(this);
        InitializeTree(root);
    }

    public static TableSchemaDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("TableSchema file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<TableSchema>(filePath)
            ?? throw new InvalidOperationException($"TableSchema deserialized to null: {filePath}");
        return new TableSchemaDocumentViewModel(filePath, root);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        TableSchema => "DefTableSchema",
        DbFieldCollection or DbField => "IconColumn",
        DbTableIndex { PrimaryKey: true } => "IconLock",
        DbTableIndexCollection or DbTableIndex => "IconKey",
        IndexField => "IconDot",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddField))]
    private void AddField()
    {
        var folder = FindAncestor<DbFieldCollection>(SelectedTreeNode) ?? FolderOf<DbFieldCollection>();
        if (folder is null) return;
        var name = UniqueKey(Root.Fields!.Select(f => f.FieldName), "new_field");
        var field = new DbField(name, "New field", FieldDbType.String);
        Root.Fields!.Add(field);
        AddNode(folder, field);
        StatusText = L("Status_AddedNamed", "DbField", name);
    }
    private bool CanAddField() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddIndex))]
    private void AddIndex()
    {
        var folder = FindAncestor<DbTableIndexCollection>(SelectedTreeNode) ?? FolderOf<DbTableIndexCollection>();
        if (folder is null) return;
        var name = UniqueKey(Root.Indexes!.Select(ix => ix.Name), "IX_new");
        var index = new DbTableIndex { Name = name };
        Root.Indexes!.Add(index);
        AddNode(folder, index);
        StatusText = L("Status_AddedNamed", "DbTableIndex", name);
    }
    private bool CanAddIndex() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddIndexField))]
    private void AddIndexField()
    {
        var indexNode = FindAncestor<DbTableIndex>(SelectedTreeNode);
        if (indexNode?.Value is not DbTableIndex index) return;
        var first = (Root.Fields ?? Enumerable.Empty<DbField>()).FirstOrDefault();
        var fname = first?.FieldName ?? "field";
        var existing = (index.IndexFields ?? Enumerable.Empty<IndexField>()).Select(i => i.FieldName);
        var key = UniqueKey(existing, fname);
        var ifld = new IndexField(key, SortDirection.Asc);
        index.IndexFields!.Add(ifld);
        AddNode(indexNode, ifld);
        StatusText = L("Status_AddedNamed", "IndexField", key);
    }
    private bool CanAddIndexField() => FindAncestor<DbTableIndex>(SelectedTreeNode) is not null;

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        DbField f => () => Root.Fields!.Remove(f),
        DbTableIndex ix => () => Root.Indexes!.Remove(ix),
        IndexField ifld when node.Parent?.Value is DbTableIndex parentIx => () => parentIx.IndexFields!.Remove(ifld),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(Root.TableName))
            issues.Add(new(ValidationSeverity.Error, "TableSchema", "TableName cannot be empty."));

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in Root.Fields ?? Enumerable.Empty<DbField>())
        {
            var path = string.IsNullOrEmpty(field.FieldName) ? "Fields[?]" : $"Fields.{field.FieldName}";
            if (string.IsNullOrWhiteSpace(field.FieldName))
                issues.Add(new(ValidationSeverity.Error, path, "DbField.FieldName cannot be empty."));
            else if (!fieldNames.Add(field.FieldName))
                issues.Add(new(ValidationSeverity.Error, path, $"FieldName '{field.FieldName}' is a duplicate."));
        }

        var indexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int pkCount = 0;
        foreach (var index in Root.Indexes ?? Enumerable.Empty<DbTableIndex>())
        {
            var ixPath = string.IsNullOrEmpty(index.Name) ? "Indexes[?]" : $"Indexes.{index.Name}";
            if (string.IsNullOrWhiteSpace(index.Name))
                issues.Add(new(ValidationSeverity.Error, ixPath, "DbTableIndex.Name cannot be empty."));
            else if (!indexNames.Add(index.Name))
                issues.Add(new(ValidationSeverity.Error, ixPath, $"Index name '{index.Name}' is a duplicate."));
            if (index.PrimaryKey) pkCount++;

            foreach (var ifld in index.IndexFields ?? Enumerable.Empty<IndexField>())
            {
                var ifPath = $"{ixPath}.{ifld.FieldName}";
                if (string.IsNullOrWhiteSpace(ifld.FieldName))
                    issues.Add(new(ValidationSeverity.Error, ifPath, "IndexField.FieldName cannot be empty."));
                else if (!fieldNames.Contains(ifld.FieldName))
                    issues.Add(new(ValidationSeverity.Error, ifPath,
                        $"IndexField '{ifld.FieldName}' is not declared in the Fields list."));
            }
        }
        if (pkCount > 1)
            issues.Add(new(ValidationSeverity.Error, "Indexes",
                $"PrimaryKey index count is {pkCount}; only 0 or 1 is allowed."));
        return issues;
    }
}
