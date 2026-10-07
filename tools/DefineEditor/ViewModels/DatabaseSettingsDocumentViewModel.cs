using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using CommunityToolkit.Mvvm.Input;
using Polhem.DefineEditor.Views;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="DatabaseSettings"/>. Two top-level groups (Servers / Items), with the
/// selected server or item in the property grid; a server's or item's context menu opens the
/// connection-string paste-and-split dialog. Validation runs the dedicated
/// <see cref="DatabaseSettingsValidator"/>.
/// </summary>
public sealed partial class DatabaseSettingsDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public DatabaseSettings Root { get; }

    protected override object RootObject => Root;

    public override string TabIcon => "DefDatabaseSettings";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    /// <summary>A DatabaseItem's ServerId suggests the ids of the servers in this file.</summary>
    public override Func<PropertyDescriptor, object, IReadOnlyList<string>?>? ValueSuggestionProvider => Suggest;

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

    /// <summary>
    /// Opens the paste-and-split dialog for the selected server or item and applies what the user confirms.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPasteConnectionString))]
    private async Task PasteConnectionStringAsync()
    {
        var target = SelectedTreeNode?.Value;
        var databaseType = target switch
        {
            DatabaseServer server => server.DatabaseType,
            DatabaseItem item => item.DatabaseType,
            _ => (DatabaseType?)null,
        };
        if (databaseType is null) return;
        var owner = GetOwnerWindow();
        if (owner is null) return; // smoke / headless
        var result = await ConnectionStringDialog.ShowAsync(owner,
            new ConnectionStringDialogViewModel(databaseType.Value, forItem: target is DatabaseItem));
        if (result is not null)
            ApplyConnectionString(result);
    }

    private bool CanPasteConnectionString() => SelectedTreeNode?.Value is DatabaseServer or DatabaseItem;

    /// <summary>
    /// Writes a parse result without errors to the selected server or item: the rewritten connection string, and the
    /// user id, password and (for an item) database name the string carried.
    /// </summary>
    /// <remarks>Nothing about the values reaches the status line, since one of them is a password.</remarks>
    public void ApplyConnectionString(ConnectionStringParseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsOk) return;
        switch (SelectedTreeNode?.Value)
        {
            case DatabaseServer server:
                server.ConnectionString = result.RewrittenConnectionString;
                if (result.UserId is not null) server.UserId = result.UserId;
                if (result.Password is not null) server.Password = result.Password;
                break;
            case DatabaseItem item:
                item.ConnectionString = result.RewrittenConnectionString;
                if (result.UserId is not null) item.UserId = result.UserId;
                if (result.Password is not null) item.Password = result.Password;
                if (result.DbName is not null) item.DbName = result.DbName;
                break;
            default:
                return;
        }
        OnSelectedObjectChangedByCommand();
        StatusText = L("Status_ConnectionStringApplied");
    }

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        DatabaseServer s => () => Root.Servers!.Remove(s),
        DatabaseItem i => () => Root.Items!.Remove(i),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation() =>
        DatabaseSettingsValidator.Validate(Root);

    /// <summary>The values the grid offers: the ids of this file's servers for a DatabaseItem's ServerId.</summary>
    public IReadOnlyList<string>? Suggest(PropertyDescriptor property, object component) =>
        component is DatabaseItem && property.Name == nameof(DatabaseItem.ServerId) && SnapshotServerIds() is { Count: > 0 } ids
            ? ids
            : null;

    private IReadOnlyList<string> SnapshotServerIds() =>
        (Root.Servers ?? Enumerable.Empty<DatabaseServer>())
            .Select(s => s.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray();
}
