using System.Windows.Input;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The context-menu commands of the FormLayout tree: add a section, field, grid or
/// column where the node allows it, and delete what <see cref="FormLayoutDocumentViewModel"/>
/// can delete. The commands act on the selected node, which the tree selects on right-click.
/// </summary>
public sealed class FormLayoutCommandProvider(FormLayoutDocumentViewModel document) : ITreeNodeCommandProvider
{
    public IReadOnlyList<TreeNodeCommand> GetCommands(ObjectTreeNode node)
    {
        var commands = new List<TreeNodeCommand>();
        switch (node)
        {
            case { IsFolder: true, Value: LayoutSectionCollection }:
                commands.Add(Create("FormLayout_AddSection", "IconSection", document.AddSectionCommand));
                break;
            case { Value: LayoutSection }:
                commands.Add(Create("FormLayout_AddField", "IconColumn", document.AddLayoutFieldCommand));
                break;
            case { IsFolder: true, Value: LayoutGridCollection }:
                commands.Add(Create("FormLayout_AddGrid", "IconGrid", document.AddGridCommand));
                break;
            case { Value: LayoutGrid }:
                commands.Add(Create("FormLayout_AddColumn", "IconColumn", document.AddLayoutColumnCommand));
                break;
        }

        if (document.DeleteCommand.CanExecute(null))
        {
            commands.Add(new TreeNodeCommand(Text("Action_Delete"), () => document.DeleteCommand.Execute(null))
            {
                IconKey = "IconDelete",
                BeginsGroup = true,
            });
        }
        return commands;
    }

    private static TreeNodeCommand Create(string labelKey, string iconKey, ICommand command) =>
        new(Text(labelKey), () => command.Execute(null))
        {
            IconKey = iconKey,
            IsEnabled = command.CanExecute(null),
        };

    private static string Text(string key) => LocalizationService.Current[key];
}
