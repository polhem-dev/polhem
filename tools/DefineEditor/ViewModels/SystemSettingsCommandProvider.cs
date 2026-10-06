using Polhem.Definition.Collections;
using Polhem.Definition.ObjectTree;
using Polhem.Definition.Settings;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the SystemSettings tree.</summary>
public sealed class SystemSettingsCommandProvider(SystemSettingsDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node switch
    {
        { IsFolder: true, Value: PropertyCollection } => [Create("SystemSettings_AddProperty", "IconAdd", document.AddPropertyCommand)],
        _ => [],
    };
}
