using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A <see cref="TreeNodeCommand"/> as a toolbar button binds to it.</summary>
public sealed class TreeToolbarItem
{
    /// <param name="command">The command.</param>
    /// <param name="shortcut">The key that runs the command from the tree, shown in the tooltip; <c>null</c> for
    /// none.</param>
    public TreeToolbarItem(TreeNodeCommand command, string? shortcut = null)
    {
        Label = command.Label;
        ToolTip = shortcut is null ? command.Label : $"{command.Label} ({shortcut})";
        IconKey = command.IconKey;
        IsEnabled = command.IsEnabled;
        Command = new RelayCommand(command.Execute, () => command.IsEnabled);
    }

    /// <summary>The localized text of the command, also its accessible name.</summary>
    public string Label { get; }

    /// <summary>The tooltip: the text of the command, and its key when it has one.</summary>
    public string ToolTip { get; }

    /// <summary>The key of the command's icon, or <c>null</c> for none.</summary>
    public string? IconKey { get; }

    /// <summary>Whether the command can run now; a disabled command stays on the toolbar, greyed out.</summary>
    public bool IsEnabled { get; }

    /// <summary>Runs the command when it is enabled.</summary>
    public ICommand Command { get; }
}
