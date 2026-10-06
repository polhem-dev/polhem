using Polhem.Core.Serialization;
using Polhem.Definition.Settings;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using CommunityToolkit.Mvvm.Input;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Editor for <see cref="PermissionModels"/>. Two-level tree:
/// PermissionModels → PermissionModel[] → PermissionRule[]. Validation wraps the
/// built-in <see cref="PermissionModels.Validate"/> plus duplicate / empty
/// ModelId checks the framework method doesn't catch.
/// </summary>
public sealed partial class PermissionModelsDocumentViewModel : ObjectTreeDocumentViewModelBase
{
    public PermissionModels Root { get; }

    protected override object RootObject => Root;

    public override string TabIcon => "DefPermissionModels";

    public override ITreeNodeCommandProvider CommandProvider { get; }

    private PermissionModelsDocumentViewModel(string filePath, PermissionModels root)
        : base(filePath, "PermissionModels", keyText: string.Empty, new ObjectTreeOptions { ExpandDepth = 1 })
    {
        Root = root;
        CommandProvider = new PermissionModelsCommandProvider(this);
        InitializeTree(root);
    }

    public static PermissionModelsDocumentViewModel Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PermissionModels file not found.", filePath);
        var root = XmlCodec.DeserializeFromFile<PermissionModels>(filePath)
            ?? throw new InvalidOperationException($"PermissionModels deserialized to null: {filePath}");
        return new PermissionModelsDocumentViewModel(filePath, root);
    }

    public override string IconKeyFor(ObjectTreeNode node) => node.Value switch
    {
        PermissionModels => "DefPermissionModels",
        PermissionModel => "IconBox",
        PermissionRule => "IconKey",
        _ => "DefUnknown",
    };

    [RelayCommand(CanExecute = nameof(CanAddModel))]
    private void AddModel()
    {
        if (SelectedTreeNode is not { Value: PermissionModels root } rootNode)
            return;
        var modelId = UniqueKey(root.Models!.Select(m => m.ModelId), "NewModel");
        var model = new PermissionModel(modelId, "New model");
        root.Models!.Add(model);
        AddNode(rootNode, model);
        StatusText = L("Status_AddedNamed", "PermissionModel", modelId);
    }

    private bool CanAddModel() => SelectedTreeNode?.Value is PermissionModels;

    [RelayCommand(CanExecute = nameof(CanAddRule))]
    private void AddRule()
    {
        var modelNode = FindAncestor<PermissionModel>(SelectedTreeNode);
        if (modelNode?.Value is not PermissionModel model) return;
        var action = PickAvailableAction(model);
        if (action is null)
        {
            StatusText = L("Status_PermissionRuleCovered");
            return;
        }
        var rule = new PermissionRule(action.Value);
        model.Rules!.Add(rule);
        AddNode(modelNode, rule);
        StatusText = L("Status_AddedNamed", "PermissionRule", action.Value);
    }

    private bool CanAddRule() => FindAncestor<PermissionModel>(SelectedTreeNode) is not null;

    private static PermissionActions? PickAvailableAction(PermissionModel model)
    {
        var taken = new HashSet<PermissionActions>(
            (model.Rules ?? Enumerable.Empty<PermissionRule>()).Select(r => r.Action));
        foreach (var candidate in EditorOptions.PermissionActionValues)
            if (!taken.Contains(candidate)) return candidate;
        return null;
    }

    protected override Action? GetDeleteAction(ObjectTreeNode node) => node.Value switch
    {
        PermissionModel m when node.Parent?.Value is PermissionModels p => () => p.Models!.Remove(m),
        PermissionRule r when node.Parent?.Value is PermissionModel pm => () => pm.Rules!.Remove(r),
        _ => null,
    };

    protected override IReadOnlyList<ValidationIssue> PerformValidation()
    {
        var issues = new List<ValidationIssue>();
        var models = Root.Models;
        if (models is null || models.Count == 0)
        {
            issues.Add(new(ValidationSeverity.Warning, "PermissionModels", "No PermissionModel has been defined."));
        }
        else
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in models)
            {
                var path = !string.IsNullOrWhiteSpace(model.ModelId) ? model.ModelId : "(unnamed)";
                if (string.IsNullOrWhiteSpace(model.ModelId))
                    issues.Add(new(ValidationSeverity.Error, path, "ModelId cannot be empty."));
                else if (!seen.Add(model.ModelId))
                    issues.Add(new(ValidationSeverity.Error, path,
                        $"ModelId '{model.ModelId}' is a duplicate within the registry."));
            }
        }

        foreach (var err in Root.Validate())
            issues.Add(new(ValidationSeverity.Error, "PermissionModels", err));

        return issues;
    }
}
