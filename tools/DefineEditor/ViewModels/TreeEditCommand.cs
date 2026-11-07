using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A command that moves, duplicates or deletes a tree node, with the key that runs it from the tree.</summary>
/// <param name="Command">The command.</param>
/// <param name="Shortcut">The key as the tooltip shows it, for example <c>⌘D</c> on macOS and <c>Ctrl+D</c>
/// elsewhere.</param>
public sealed record TreeEditCommand(TreeNodeCommand Command, string Shortcut);
