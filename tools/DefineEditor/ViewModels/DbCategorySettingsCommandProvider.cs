using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the DbCategorySettings tree.</summary>
public sealed class DbCategorySettingsCommandProvider(DbCategorySettingsDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node.Value switch
    {
        DbCategorySettings => [Create("DbCategory_AddCategory", "IconAdd", document.AddCategoryCommand)],
        DbCategory => [Create("DbCategory_AddTable", "IconTable", document.AddTableCommand)],
        _ => [],
    };
}
