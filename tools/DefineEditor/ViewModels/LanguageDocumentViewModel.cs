using Polhem.Core.Serialization;
using Polhem.Definition.Language;
using Polhem.DefineEditor.Models;
using Polhem.Definition.ObjectTree;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="LanguageResource"/>. Tree: LanguageResource → Items
/// group → LanguageItem[]; LanguageResource → Enums group → LanguageEnum[] →
/// LanguageEnumEntry[]. Single resource = single namespace × single language.
/// </summary>
public sealed partial class LanguageDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public LanguageResource Root { get; }
    protected override object RootObject => Root;

    public override string TabIcon => "DefLanguage";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    private LanguageDocumentViewModel(string filePath, LanguageResource root)
        // The root and the Items and Enums folders start expanded.
        : base(filePath, "Language", keyText: string.IsNullOrEmpty(root.Lang) ? root.Namespace : $"{root.Lang}/{root.Namespace}",
            new ObjectTreeOptions { ExpandDepth = 2 })
    {
        Root = root;
        CommandProvider = new LanguageCommandProvider(this);
        InitializeTree(root);
    }

    public static LanguageDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Language file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<LanguageResource>(filePath)
            ?? throw new InvalidOperationException($"LanguageResource deserialized to null: {filePath}");
        return new LanguageDocumentViewModel(filePath, root);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        LanguageResource => "DefLanguage",
        LanguageItemCollection => "IconText",
        LanguageEnumCollection or LanguageEnum => "IconList",
        LanguageItem or LanguageEnumEntry => "IconDot",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        var folder = FindAncestor<LanguageItemCollection>(SelectedTreeNode) ?? FolderOf<LanguageItemCollection>();
        if (folder is null) return;
        var key = UniqueKey(Root.Items.Select(i => i.Key), "NewKey");
        var item = new LanguageItem { Key = key, Value = "New text" };
        Root.Items.Add(item);
        AddNode(folder, item);
        StatusText = L("Status_AddedNamed", "LanguageItem", key);
    }
    private bool CanAddItem() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddEnum))]
    private void AddEnum()
    {
        var folder = FindAncestor<LanguageEnumCollection>(SelectedTreeNode) ?? FolderOf<LanguageEnumCollection>();
        if (folder is null) return;
        var name = UniqueKey(Root.Enums.Select(e => e.Name), "NewEnum");
        var enumDef = new LanguageEnum { Name = name };
        Root.Enums.Add(enumDef);
        AddNode(folder, enumDef);
        StatusText = L("Status_AddedNamed", "LanguageEnum", name);
    }
    private bool CanAddEnum() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(CanAddEntry))]
    private void AddEntry()
    {
        var enumNode = FindAncestor<LanguageEnum>(SelectedTreeNode);
        if (enumNode?.Value is not LanguageEnum enumDef) return;
        var code = UniqueKey(enumDef.Entries.Select(e => e.Code), "code");
        var entry = new LanguageEnumEntry { Code = code, Text = "New entry" };
        enumDef.Entries.Add(entry);
        AddNode(enumNode, entry);
        StatusText = L("Status_AddedNamed", "LanguageEnumEntry", code);
    }
    private bool CanAddEntry() => FindAncestor<LanguageEnum>(SelectedTreeNode) is not null;

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        LanguageItem li => () => Root.Items.Remove(li),
        LanguageEnum le => () => Root.Enums.Remove(le),
        LanguageEnumEntry lee when node.Parent?.Value is LanguageEnum parentEnum => () => parentEnum.Entries.Remove(lee),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(Root.Namespace))
            issues.Add(new(ValidationSeverity.Error, "LanguageResource", "Namespace cannot be empty."));
        if (string.IsNullOrWhiteSpace(Root.Lang))
            issues.Add(new(ValidationSeverity.Error, "LanguageResource", "Lang cannot be empty (recommended: BCP-47 codes such as zh-TW / en-US)."));

        var itemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Root.Items)
        {
            var path = string.IsNullOrEmpty(item.Key) ? "Items[?]" : $"Items.{item.Key}";
            if (string.IsNullOrWhiteSpace(item.Key))
                issues.Add(new(ValidationSeverity.Error, path, "LanguageItem.Key cannot be empty."));
            else if (!itemKeys.Add(item.Key))
                issues.Add(new(ValidationSeverity.Error, path, $"Item.Key '{item.Key}' is a duplicate."));
        }

        var enumNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var enumDef in Root.Enums)
        {
            var ePath = string.IsNullOrEmpty(enumDef.Name) ? "Enums[?]" : $"Enums.{enumDef.Name}";
            if (string.IsNullOrWhiteSpace(enumDef.Name))
                issues.Add(new(ValidationSeverity.Error, ePath, "LanguageEnum.Name cannot be empty."));
            else if (!enumNames.Add(enumDef.Name))
                issues.Add(new(ValidationSeverity.Error, ePath, $"Enum.Name '{enumDef.Name}' is a duplicate."));

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in enumDef.Entries)
            {
                var entryPath = $"{ePath}.{(string.IsNullOrEmpty(entry.Code) ? "(unnamed)" : entry.Code)}";
                if (string.IsNullOrWhiteSpace(entry.Code))
                    issues.Add(new(ValidationSeverity.Error, entryPath, "Entry.Code cannot be empty."));
                else if (!codes.Add(entry.Code))
                    issues.Add(new(ValidationSeverity.Error, entryPath,
                        $"Entry.Code '{entry.Code}' is a duplicate within '{enumDef.Name}'."));
            }
        }
        return issues;
    }
}
