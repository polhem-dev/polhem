using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The context-menu commands of the FormLayout tree: add a section, field, grid or column where
/// the node allows it, then Delete, and Properties when the property grid is off the side.
/// </summary>
public sealed class FormLayoutCommandProvider(FormLayoutDocumentViewModel document) : TreeCommandProviderBase(document)
{
    protected override IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node) => node switch
    {
        { IsFolder: true, Value: LayoutSectionCollection } => [Create("FormLayout_AddSection", "IconSection", document.AddSectionCommand)],
        { Value: LayoutSection } => [Create("FormLayout_AddField", "IconColumn", document.AddLayoutFieldCommand)],
        { IsFolder: true, Value: LayoutGridCollection } => [Create("FormLayout_AddGrid", "IconGrid", document.AddGridCommand)],
        { Value: LayoutGrid } => [Create("FormLayout_AddColumn", "IconColumn", document.AddLayoutColumnCommand)],
        _ => [],
    };

    protected override IEnumerable<TreeNodeCommand> GetTrailingCommands(ObjectTreeNode node) =>
        document.IsPropertyPaneVisible || node.IsFolder
            ? []
            : [Create("FormLayout_EditProperties", "IconSettings", document.EditPropertiesCommand, beginsGroup: true)];
}
