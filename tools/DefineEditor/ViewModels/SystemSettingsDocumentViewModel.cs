using Polhem.Core.Serialization;
using Polhem.Definition.Collections;
using Polhem.Definition.Logging;
using Polhem.Definition.Settings;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="SystemSettings"/>. The 5 Configuration sub-objects and
/// the ExtendedProperties bag are mounted as fixed tree nodes; BackendConfiguration
/// also exposes its 4 nested option records (LogOptions, SecurityKeySettings,
/// Components, CacheNotifyOptions) as children. Add/Delete is only meaningful on
/// the ExtendedProperties branch.
/// </summary>
public sealed partial class SystemSettingsDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public SystemSettings Root { get; }

    protected override object RootObject => Root;

    public override string TabIcon => "DefSystemSettings";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    private SystemSettingsDocumentViewModel(string filePath, SystemSettings root)
        : base(filePath, "SystemSettings", keyText: string.Empty, new ObjectTreeOptions
        {
            ExpandDepth = 1,
            NodeBuilt = AddExtendedPropertiesFolder,
        })
    {
        Root = root;
        CommandProvider = new SystemSettingsCommandProvider(this);
        InitializeTree(root);
    }

    public static SystemSettingsDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("SystemSettings file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<SystemSettings>(filePath)
            ?? throw new InvalidOperationException($"SystemSettings deserialized to null: {filePath}");
        return new SystemSettingsDocumentViewModel(filePath, root);
    }

    /// <summary>
    /// <see cref="PropertyCollection"/> carries no <c>[TreeNode]</c> (it also sits under every layout
    /// field), so the extended properties get their folder here, as the last child of the root.
    /// </summary>
    private static void AddExtendedPropertiesFolder(ObjectTreeNode node, ObjectTreeBuilder builder)
    {
        if (node.Value is not SystemSettings settings) { return; }
        var properties = settings.ExtendedProperties!;
        var folder = new ObjectTreeNode(properties, "ExtendedProperties", isFolder: true);
        foreach (var property in properties)
            folder.Children.Add(builder.Build(property));
        node.Children.Add(folder);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        SystemSettings => "DefSystemSettings",
        CommonConfiguration => "IconSettings",
        BackendConfiguration => "IconWrench",
        LogOptions => "IconText",
        SecurityKeySettings => "IconLock",
        BackendComponents => "IconLayers",
        CacheNotifyOptions => "IconBell",
        FrontendConfiguration => "IconMonitor",
        WebsiteConfiguration => "IconGlobe",
        BackgroundServiceConfiguration => "IconClock",
        PropertyCollection => "IconList",
        Property => "IconDot",
        _ => "IconSettings",
    };

    [RelayCommand(CanExecute = nameof(CanAddProperty))]
    private void AddProperty()
    {
        var folder = FindAncestor<PropertyCollection>(SelectedTreeNode);
        if (folder is null) return;

        var name = UniqueKey(Root.ExtendedProperties!.Select(p => p.Name), "NewProperty");
        var prop = new Property { Name = name, Value = string.Empty };
        Root.ExtendedProperties!.Add(prop);
        AddNode(folder, prop);
        StatusText = L("Status_AddedNamed", "ExtendedProperty", name);
    }

    private bool CanAddProperty() => FindAncestor<PropertyCollection>(SelectedTreeNode) is not null;

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        Property p => () => Root.ExtendedProperties!.Remove(p),
        _ => null,
    };
}
