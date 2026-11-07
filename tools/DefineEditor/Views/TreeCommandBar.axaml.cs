using Avalonia.Controls;
using Polhem.DefineEditor.Converters;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.Views;

/// <summary>The toolbar above an editor's tree, with the commands of the selected node.</summary>
public partial class TreeCommandBar : UserControl
{
    public TreeCommandBar()
    {
        InitializeComponent();
    }

    // The menu items are built in code, as `ObjectTreeView` builds its context menu, so the overflow menu looks the
    // same as the context menu it repeats.
    private void OnMoreFlyoutOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout flyout || DataContext is not ObjectTreeDocumentViewModelBase document)
            return;

        flyout.ItemsSource = document.ToolbarActionItems
            .Select(item => new MenuItem
            {
                Header = item.Label,
                Command = item.Command,
                IsEnabled = item.IsEnabled,
                Icon = item.IconKey is { } key && TreeIconSelector.Resolve(key) is { } geometry
                    ? new PathIcon { Data = geometry, Width = 14, Height = 14 }
                    : null,
            })
            .ToList();
    }
}
