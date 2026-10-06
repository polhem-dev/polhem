using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the DatabaseSettings tree.</summary>
public sealed class DatabaseSettingsCommandProvider(DatabaseSettingsDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node switch
    {
        { IsFolder: true, Value: DatabaseServerCollection } => [Create("DatabaseSettings_AddServer", "IconDatabase", document.AddServerCommand)],
        { IsFolder: true, Value: DatabaseItemCollection } => [Create("DatabaseSettings_AddItem", "IconAdd", document.AddItemCommand)],
        _ => [],
    };
}
