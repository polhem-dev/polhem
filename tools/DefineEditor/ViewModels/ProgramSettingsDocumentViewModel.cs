using Polhem.Core.Serialization;
using Polhem.Definition.Settings;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="ProgramSettings"/>. Flat tree: ProgramSettings → ProgramItem[].
/// Validation: empty or duplicate ProgIds across the whole registry.
/// </summary>
public sealed partial class ProgramSettingsDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public ProgramSettings Root { get; }

    protected override object RootObject => Root;

    public override string TabIcon => "DefProgramSettings";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    private ProgramSettingsDocumentViewModel(string filePath, ProgramSettings root)
        : base(filePath, "ProgramSettings", keyText: string.Empty, new ObjectTreeOptions { ExpandDepth = 1 })
    {
        Root = root;
        CommandProvider = new ProgramSettingsCommandProvider(this);
        InitializeTree(root);
    }

    public static ProgramSettingsDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("ProgramSettings file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<ProgramSettings>(filePath)
            ?? throw new InvalidOperationException($"ProgramSettings deserialized to null: {filePath}");
        return new ProgramSettingsDocumentViewModel(filePath, root);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        ProgramSettings => "DefProgramSettings",
        ProgramItem => "IconBox",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddProgram))]
    private void AddProgram()
    {
        if (SelectedTreeNode is not { Value: ProgramSettings root } rootNode)
            return;
        var id = UniqueKey(root.Items!.Select(p => p.ProgId), "NewProgram");
        var program = new ProgramItem { ProgId = id, DisplayName = "New program" };
        root.Items!.Add(program);
        AddNode(rootNode, program);
        StatusText = L("Status_AddedNamed", "ProgramItem", id);
    }

    private bool CanAddProgram() => SelectedTreeNode?.Value is ProgramSettings;

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        ProgramItem prog when node.Parent?.Value is ProgramSettings p => () => p.Items!.Remove(prog),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
    {
        var issues = new List<ValidationIssue>();
        var items = Root.Items;
        if (items is null || items.Count == 0)
        {
            issues.Add(new(ValidationSeverity.Warning, "ProgramSettings", "No ProgramItem has been registered."));
            return issues;
        }

        // The registry is flat, so duplicate detection is global: the progId is the registry's key.
        var seenProgIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in items)
        {
            var path = string.IsNullOrEmpty(program.ProgId) ? "(unnamed)" : program.ProgId;
            if (string.IsNullOrWhiteSpace(program.ProgId))
                issues.Add(new(ValidationSeverity.Error, path, "ProgramItem.ProgId cannot be empty."));
            else if (!seenProgIds.Add(program.ProgId))
                issues.Add(new(ValidationSeverity.Error, path,
                    $"ProgramItem.ProgId '{program.ProgId}' is a duplicate."));
        }
        return issues;
    }
}
