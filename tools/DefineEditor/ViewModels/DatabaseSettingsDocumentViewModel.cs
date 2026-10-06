using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="DatabaseSettings"/>. Two top-level groups (Servers /
/// Items); selection on a Server or Item yields a wrapper editor with proxy
/// fields plus the connection-string paste-and-split UI. Validation runs the
/// dedicated <see cref="DatabaseSettingsValidator"/>.
/// </summary>
public sealed partial class DatabaseSettingsDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public DatabaseSettings Root { get; }

    protected override object RootObject => Root;

    public override string TabIcon => "DefDatabaseSettings";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    public override object? SelectedEditorContext => SelectedTreeNode switch
    {
        { Value: DatabaseServer server } => new DatabaseServerEditor(server, () => IsDirty = true),
        { Value: DatabaseItem item } => new DatabaseItemEditor(item, SnapshotServerIds(), () => IsDirty = true),
        _ => base.SelectedEditorContext,
    };

    private DatabaseSettingsDocumentViewModel(string filePath, DatabaseSettings root)
        // The root and the two folders start expanded.
        : base(filePath, "DatabaseSettings", keyText: string.Empty, new ObjectTreeOptions { ExpandDepth = 2 })
    {
        Root = root;
        CommandProvider = new DatabaseSettingsCommandProvider(this);
        InitializeTree(root);
    }

    public static DatabaseSettingsDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("DatabaseSettings file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<DatabaseSettings>(filePath)
            ?? throw new InvalidOperationException($"DatabaseSettings deserialized to null: {filePath}");
        return new DatabaseSettingsDocumentViewModel(filePath, root);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        DatabaseSettings => "DefDatabaseSettings",
        DatabaseServerCollection or DatabaseServer => "IconServer",
        DatabaseItemCollection or DatabaseItem => "IconDatabase",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddServer))]
    private void AddServer()
    {
        var folder = FindAncestor<DatabaseServerCollection>(SelectedTreeNode) ?? FolderOf<DatabaseServerCollection>();
        if (folder is null) return;
        var id = UniqueKey(Root.Servers!.Select(s => s.Id), "new_server");
        var server = new DatabaseServer { Id = id, DisplayName = "New server", DatabaseType = DatabaseType.SQLServer };
        Root.Servers!.Add(server);
        AddNode(folder, server);
        StatusText = L("Status_AddedNamed", "DatabaseServer", id);
    }

    private bool CanAddServer() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        var folder = FindAncestor<DatabaseItemCollection>(SelectedTreeNode) ?? FolderOf<DatabaseItemCollection>();
        if (folder is null) return;
        var id = UniqueKey(Root.Items!.Select(i => i.Id), "new_item");
        var item = new DatabaseItem
        {
            Id = id,
            DisplayName = "New database",
            DatabaseType = DatabaseType.SQLServer,
        };
        Root.Items!.Add(item);
        AddNode(folder, item);
        StatusText = L("Status_AddedNamed", "DatabaseItem", id);
    }

    private bool CanAddItem() => SelectedTreeNode is not null;

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        DatabaseServer s => () => Root.Servers!.Remove(s),
        DatabaseItem i => () => Root.Items!.Remove(i),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation() =>
        DatabaseSettingsValidator.Validate(Root);

    private IReadOnlyList<string> SnapshotServerIds() =>
        (Root.Servers ?? Enumerable.Empty<DatabaseServer>())
            .Select(s => s.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray();
}
