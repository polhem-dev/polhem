using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the PermissionModels tree.</summary>
public sealed class PermissionModelsCommandProvider(PermissionModelsDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node.Value switch
    {
        PermissionModels => [Create("Permission_AddModel", "IconKey", document.AddModelCommand)],
        PermissionModel => [Create("Permission_AddRule", "IconAdd", document.AddRuleCommand)],
        _ => [],
    };
}
