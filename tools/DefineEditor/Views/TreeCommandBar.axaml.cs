using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Polhem.DefineEditor.Converters;
using Polhem.DefineEditor.ViewModels;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.Views;

/// <summary>The toolbar above an editor's tree, with the commands of the selected node and Find in tree.</summary>
public partial class TreeCommandBar : UserControl
{
    private UserControl? _view;

    public TreeCommandBar()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            // Cmd/Ctrl+F anywhere in the editor goes to the find box.
            _view = this.FindAncestorOfType<UserControl>();
            _view?.AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Tunnel);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _view?.RemoveHandler(KeyDownEvent, OnViewKeyDown);
            _view = null;
        };
    }

    private static KeyModifiers CommandModifier => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F || e.KeyModifiers != CommandModifier) return;
        FindBox.Focus();
        FindBox.SelectAll();
        e.Handled = true;
    }

    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ObjectTreeDocumentViewModelBase document) return;
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.Shift:
                document.FindPreviousCommand.Execute(null);
                RevealSelectedNode(focus: false);
                break;
            case Key.Enter:
                document.FindNextCommand.Execute(null);
                RevealSelectedNode(focus: false);
                break;
            case Key.Escape:
                // Leaves the selection on the match it found, and hands the focus back to the tree.
                document.TreeFindText = string.Empty;
                RevealSelectedNode(focus: true);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // Typing selects the first match; scroll it into view once the tree has expanded its ancestors.
    private void OnFindTextChanged(object? sender, TextChangedEventArgs e) => RevealSelectedNode(focus: false);

    private void RevealSelectedNode(bool focus) =>
        Dispatcher.UIThread.Post(() =>
        {
            var tree = _view?.GetVisualDescendants().OfType<ObjectTreeView>().FirstOrDefault();
            if (tree?.SelectedItem is not { } item || tree.TreeContainerFromItem(item) is not { } container) return;
            container.BringIntoView();
            if (focus) container.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);

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
