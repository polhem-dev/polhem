using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the FormSchema tree.</summary>
public sealed class FormSchemaCommandProvider(FormSchemaDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node)
    {
        if (node.Value is FormSchema)
            return [Create("FormSchema_GenerateFormLayout", "DefFormLayout", document.GenerateFormLayoutCommand)];
        if (node is { IsFolder: true, Value: FormTableCollection })
            return [Create("FormSchema_AddTable", "IconTable", document.AddTableCommand)];
        if (node.Value is FormTable)
            return [Create("FormSchema_AddField", "IconAdd", document.AddFieldCommand)];
        if (FormSchemaDocumentViewModel.IsRelationGroup(node))
            return [Create("FormSchema_AddRelationMapping", "IconLink", document.AddRelationMappingCommand)];
        if (FormSchemaDocumentViewModel.IsLookupGroup(node))
            return [Create("FormSchema_AddLookupMapping", "IconLink", document.AddLookupMappingCommand)];
        if (FormSchemaDocumentViewModel.IsListItemsGroup(node))
            return [Create("FormSchema_AddListItem", "IconList", document.AddListItemCommand)];
        return [];
    }
}
