using System.Windows.Input;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The commands of an editor's tree, for its context menu and its toolbar. A subclass lists the Add commands and the
/// other actions that fit a node; Delete follows in a group of its own, and a subclass may add commands after it.
/// The commands act on the selected node, which the tree selects on right-click.
/// </summary>
public abstract class TreeCommandProviderBase(ObjectTreeDocumentViewModelBase document) : ITreeNodeCommandProvider
{
    /// <summary>
    /// The context menu: the node's Add commands, its actions, Delete when the node can be deleted, then the trailing
    /// commands.
    /// </summary>
    public IReadOnlyList<TreeNodeCommand> GetCommands(ObjectTreeNode node)
    {
        var commands = new List<TreeNodeCommand>(GetAddCommands(node));
        commands.AddRange(GetActionCommands(node));
        if (document.DeleteCommand.CanExecute(null))
            commands.Add(CreateDelete(beginsGroup: true));
        commands.AddRange(GetTrailingCommands(node));
        return commands;
    }

    /// <summary>
    /// The toolbar: the Add commands of <paramref name="node"/> and of its ancestors, Delete, and the node's actions
    /// and trailing commands.
    /// </summary>
    /// <remarks>
    /// An ancestor's Add command is listed only when it can run from the selection, since it then adds next to the
    /// selected node (adding a field while another field is selected). The node's own Add commands are listed even
    /// when disabled, so the toolbar shows that they exist. Commands that share a label are listed once.
    /// </remarks>
    public TreeToolbarCommands GetToolbarCommands(ObjectTreeNode node)
    {
        var add = new List<TreeNodeCommand>();
        var labels = new HashSet<string>(StringComparer.Ordinal);
        for (var current = node; current != null; current = current.Parent)
        {
            foreach (var command in GetAddCommands(current))
            {
                if ((ReferenceEquals(current, node) || command.IsEnabled) && labels.Add(command.Label))
                    add.Add(command);
            }
        }

        var actions = new List<TreeNodeCommand>(GetActionCommands(node));
        actions.AddRange(GetTrailingCommands(node).Select(WithoutGroup));
        return new TreeToolbarCommands(add, CreateDelete(beginsGroup: false), actions);
    }

    /// <summary>The commands that add a child to <paramref name="node"/>, in menu order.</summary>
    protected abstract IEnumerable<TreeNodeCommand> GetAddCommands(ObjectTreeNode node);

    /// <summary>
    /// The commands for <paramref name="node"/> that neither add a child nor delete it, such as generating or
    /// exporting, in menu order. Default: none.
    /// </summary>
    protected virtual IEnumerable<TreeNodeCommand> GetActionCommands(ObjectTreeNode node) => [];

    /// <summary>The commands that follow Delete for <paramref name="node"/>, in menu order. Default: none.</summary>
    protected virtual IEnumerable<TreeNodeCommand> GetTrailingCommands(ObjectTreeNode node) => [];

    /// <summary>A menu command that runs <paramref name="command"/>, enabled as it is.</summary>
    protected static TreeNodeCommand Create(string labelKey, string iconKey, ICommand command, bool beginsGroup = false) =>
        new(Text(labelKey), () => command.Execute(null))
        {
            IconKey = iconKey,
            IsEnabled = command.CanExecute(null),
            BeginsGroup = beginsGroup,
        };

    private TreeNodeCommand CreateDelete(bool beginsGroup) =>
        Create("Action_Delete", "IconDelete", document.DeleteCommand, beginsGroup);

    // The overflow menu lists the actions and the trailing commands as one group.
    private static TreeNodeCommand WithoutGroup(TreeNodeCommand command) =>
        new(command.Label, command.Execute) { IconKey = command.IconKey, IsEnabled = command.IsEnabled };

    private static string Text(string key) => LocalizationService.Current[key];
}
