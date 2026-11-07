using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The commands an editor's toolbar shows for the selected tree node, split by what they do.
/// </summary>
/// <param name="Add">The commands that add a child, nearest node first: the selected node's own, then those of its
/// ancestors that can run from the selection, so adding a sibling needs no trip to the parent first.</param>
/// <param name="Delete">Delete, shown whether or not the selected node can be deleted.</param>
/// <param name="Actions">The selected node's other commands, which the toolbar keeps in an overflow menu.</param>
public sealed record TreeToolbarCommands(
    IReadOnlyList<TreeNodeCommand> Add,
    TreeNodeCommand Delete,
    IReadOnlyList<TreeNodeCommand> Actions);
