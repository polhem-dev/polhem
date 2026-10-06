using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the MenuSettings tree.</summary>
public sealed class MenuSettingsCommandProvider(MenuSettingsDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node.Value switch
    {
        MenuSettings or MenuFolder =>
        [
            Create("Menu_AddFolder", "IconAdd", document.AddFolderCommand),
            Create("Menu_AddEntry", "IconBox", document.AddEntryCommand),
        ],
        _ => [],
    };
}
