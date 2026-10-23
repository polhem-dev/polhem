using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the FormSchema tree.</summary>
public sealed class FormSchemaCommandProvider(FormSchemaDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node)
    {
        if (node.Value is FormSchema)
            return
            [
                Create("FormSchema_GenerateFormLayout", "DefFormLayout", document.GenerateFormLayoutCommand),
                Create("FormSchema_ExportWorkbook", "IconTable", document.ExportWorkbookCommand),
                Create("FormSchema_SyncDefinitions", "IconSync", document.SyncDefinitionsCommand),
                Create("FormSchema_UpgradeTables", "IconDatabase", document.UpgradeTablesCommand),
                Create("FormSchema_PreviewUpgradeTables", "IconDatabase", document.PreviewUpgradeTablesCommand),
            ];
        if (node is { IsFolder: true, Value: FormTableCollection })
            return [Create("FormSchema_AddTable", "IconTable", document.AddTableCommand)];
        if (node.Value is FormTable)
            return [Create("FormSchema_AddField", "IconAdd", document.AddFieldCommand)];
        if (node is { IsFolder: true, Value: FormRuleCollection } || node.Value is FormRule)
            return [Create("FormSchema_AddRule", "IconAdd", document.AddRuleCommand)];
        if (FormSchemaDocumentViewModel.IsRelationGroup(node))
            return [Create("FormSchema_AddRelationMapping", "IconLink", document.AddRelationMappingCommand)];
        if (FormSchemaDocumentViewModel.IsLookupGroup(node))
            return [Create("FormSchema_AddLookupMapping", "IconLink", document.AddLookupMappingCommand)];
        return [];
    }
}
