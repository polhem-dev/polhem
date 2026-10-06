using Polhem.Core.Serialization;
using Polhem.Definition.Settings;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="MenuSettings"/>. Arbitrarily deep tree:
/// MenuSettings → (MenuFolder | MenuEntry)*, folders owning children.
/// Validation: empty or duplicate node Ids across the whole tree, empty ProgIds,
/// and — when the sibling ProgramSettings.xml is present — entries pointing at an
/// unregistered progId.
/// </summary>
public sealed partial class MenuSettingsDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public MenuSettings Root { get; }

    protected override object RootObject => Root;

    public override string TabIcon => "DefMenuSettings";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    private MenuSettingsDocumentViewModel(string filePath, MenuSettings root)
        : base(filePath, "MenuSettings", keyText: string.Empty, new ObjectTreeOptions
        {
            ExpandDepth = 1,
            // Folders start expanded at any depth, so the whole menu is visible on open.
            NodeBuilt = (node, _) => { if (node.Value is MenuFolder) node.IsExpanded = true; },
        })
    {
        Root = root;
        CommandProvider = new MenuSettingsCommandProvider(this);
        InitializeTree(root);
    }

    public static MenuSettingsDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("MenuSettings file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<MenuSettings>(filePath)
            ?? throw new InvalidOperationException($"MenuSettings deserialized to null: {filePath}");
        // Deliberately not EnsureValid here: an editor must be able to open a broken definition in
        // order to fix it. The same problems are reported through the validation pane instead.
        return new MenuSettingsDocumentViewModel(filePath, root);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        MenuSettings => "DefMenuSettings",
        MenuFolder => "DefCategory",
        MenuEntry => "IconBox",
        _ => "DefUnknown",
    };

    private static MenuNodeCollection? OwnedItems(ObjectTreeNode? node) => node?.Value switch
    {
        MenuSettings settings => settings.Items,
        MenuFolder folder => folder.Items,
        _ => null,
    };

    [RelayCommand(CanExecute = nameof(CanAddNode))]
    private void AddFolder() => AddMenuNode(isFolder: true);

    [RelayCommand(CanExecute = nameof(CanAddNode))]
    private void AddEntry() => AddMenuNode(isFolder: false);

    private void AddMenuNode(bool isFolder)
    {
        var ownerNode = SelectedTreeNode;
        if (OwnedItems(ownerNode) is not { } owner || ownerNode is null) { return; }

        // Ids are unique across the whole tree, not merely among siblings, so the candidate is
        // checked against every existing node.
        var id = UniqueKey(Root.EnumerateNodes().Select(n => n.Id), isFolder ? "new-folder" : "new-entry");
        MenuNodeBase node = isFolder
            ? new MenuFolder(id, "New folder")
            : new MenuEntry(id, string.Empty, "New entry");
        owner.Add(node);
        AddNode(ownerNode, node);
        StatusText = L("Status_AddedNamed", isFolder ? "MenuFolder" : "MenuEntry", id);
    }

    /// <summary>Whether the selected node can hold menu nodes: the root or a folder.</summary>
    public bool CanAddNode() => OwnedItems(SelectedTreeNode) is not null;

    protected override Action? GetDeleteAction(ObjectTreeNode node)
    {
        if (node.Value is not MenuNodeBase target) { return null; }
        return OwnedItems(node.Parent) is { } owner ? () => owner.Remove(target) : null;
    }

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
    {
        var issues = new List<ValidationIssue>();
        if (Root.Items is null || Root.Items.Count == 0)
        {
            issues.Add(new(ValidationSeverity.Warning, "MenuSettings", "The menu has no nodes."));
            return issues;
        }

        // The registry lives beside the menu in a DefinePath, so referential integrity can be
        // checked here. It is skipped rather than reported when the file is absent or in the old
        // layout: opening the menu editor is not the place to fail over the registry's state.
        ProgramSettings? registry = TryLoadRegistry();

        foreach (var problem in Root.Validate(registry))
            issues.Add(new(ValidationSeverity.Error, "MenuSettings", problem));

        foreach (var folder in Root.EnumerateNodes().OfType<MenuFolder>())
        {
            if (folder.Items is null || folder.Items.Count == 0)
                issues.Add(new(ValidationSeverity.Warning, folder.Id, $"MenuFolder '{folder.Id}' is empty."));
        }

        return issues;
    }

    private ProgramSettings? TryLoadRegistry()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrEmpty(dir)) { return null; }
        var registryPath = Path.Combine(dir, "ProgramSettings.xml");
        if (!File.Exists(registryPath)) { return null; }

        try
        {
            return XmlCodec.DeserializeFromFile<ProgramSettings>(registryPath);
        }
        catch (InvalidOperationException)
        {
            // Unreadable or legacy-layout registry: the ProgramSettings editor reports that, and
            // the menu's own structural findings are still worth showing.
            return null;
        }
    }
}
