using Polhem.Definition.Language;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>The context-menu commands of the language resource tree.</summary>
public sealed class LanguageCommandProvider(LanguageDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node switch
    {
        { IsFolder: true, Value: LanguageItemCollection } => [Create("Language_AddItem", "IconText", document.AddItemCommand)],
        { IsFolder: true, Value: LanguageEnumCollection } => [Create("Language_AddEnum", "IconList", document.AddEnumCommand)],
        { Value: LanguageEnum } => [Create("Language_AddEntry", "IconAdd", document.AddEntryCommand)],
        _ => [],
    };
}
