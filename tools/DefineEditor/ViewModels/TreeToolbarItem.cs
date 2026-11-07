using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Polhem.Definition.ObjectTree;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>A <see cref="TreeNodeCommand"/> as a toolbar button binds to it.</summary>
public sealed class TreeToolbarItem
{
    public TreeToolbarItem(TreeNodeCommand command)
    {
        Label = command.Label;
        IconKey = command.IconKey;
        IsEnabled = command.IsEnabled;
        Command = new RelayCommand(command.Execute, () => command.IsEnabled);
    }

    /// <summary>The localized text of the command, also its tooltip and accessible name.</summary>
    public string Label { get; }

    /// <summary>The key of the command's icon, or <c>null</c> for none.</summary>
    public string? IconKey { get; }

    /// <summary>Whether the command can run now; a disabled command stays on the toolbar, greyed out.</summary>
    public bool IsEnabled { get; }

    /// <summary>Runs the command when it is enabled.</summary>
    public ICommand Command { get; }
}
