using Polhem.Definition.Database;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the TableSchema tree.</summary>
public sealed class TableSchemaCommandProvider(TableSchemaDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node switch
    {
        { IsFolder: true, Value: DbFieldCollection } => [Create("TableSchema_AddField", "IconColumn", document.AddFieldCommand)],
        { IsFolder: true, Value: DbTableIndexCollection } => [Create("TableSchema_AddIndex", "IconKey", document.AddIndexCommand)],
        { Value: DbTableIndex } => [Create("TableSchema_AddIndexField", "IconColumn", document.AddIndexFieldCommand)],
        _ => [],
    };
}
