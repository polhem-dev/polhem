using System.Windows.Input;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Context-menu commands of an editor's tree: the subclass lists the Add commands that fit the
/// node, and Delete follows in a group of its own whenever the document can delete the node.
/// The commands act on the selected node, which the tree selects on right-click.
/// </summary>
public abstract class TreeCommandProviderBase(ObjectTreeDocumentViewModelBase document) : ITreeNodeCommandProvider
{
    public IReadOnlyList<TreeNodeCommand> GetCommands(ObjectTreeNode node)
    {
        var commands = new List<TreeNodeCommand>(GetAddCommands(node));
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

    /// <summary>The Add commands for <paramref name="node"/>, in menu order.</summary>
    protected abstract IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node);

    /// <summary>A menu command that runs <paramref name="command"/>, enabled as it is.</summary>
    protected static TreeNodeCommand Create(string labelKey, string iconKey, ICommand command) =>
        new(Text(labelKey), () => command.Execute(null))
        {
            IconKey = iconKey,
            IsEnabled = command.CanExecute(null),
        };

    private static string Text(string key) => LocalizationService.Current[key];
}
