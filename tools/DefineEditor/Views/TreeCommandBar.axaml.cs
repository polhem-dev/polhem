using Avalonia;
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
    private ObjectTreeDocumentViewModelBase? _document;

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
        // The XML has a find and an undo of its own: keys pressed in it, or while the tree is not shown, are its.
        var inXml = (e.Source as Visual)?.FindAncestorOfType<XmlDocumentPane>(includeSelf: true) is not null;
        if (inXml || DataContext is ObjectTreeDocumentViewModelBase { IsDesignVisible: false })
            return;

        if (e.Key == Key.F && e.KeyModifiers == CommandModifier)
        {
            FindBox.Focus();
            FindBox.SelectAll();
            e.Handled = true;
            return;
        }

        // A text box keeps Undo and Redo for its own typing; elsewhere in the editor they undo document edits.
        if (e.Source is TextBox || (e.Source as Visual)?.FindAncestorOfType<TextBox>() is not null
            || DataContext is not ObjectTreeDocumentViewModelBase document)
        {
            return;
        }
        var command = (e.Key, e.KeyModifiers) switch
        {
            (Key.Z, var m) when m == CommandModifier => document.UndoCommand,
            (Key.Z, var m) when m == (CommandModifier | KeyModifiers.Shift) => document.RedoCommand,
            (Key.Y, KeyModifiers.Control) when !OperatingSystem.IsMacOS() => document.RedoCommand,
            _ => null,
        };
        if (command is null) return;
        if (command.CanExecute(null))
        {
            command.Execute(null);
            RevealSelectedNode(focus: true);
        }
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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_document is not null) _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document = DataContext as ObjectTreeDocumentViewModelBase;
        if (_document is not null) _document.PropertyChanged += OnDocumentPropertyChanged;
        FillMoreFlyout();
    }

    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ObjectTreeDocumentViewModelBase.ToolbarActionItems)) FillMoreFlyout();
    }

    // The menu items are built in code, as `ObjectTreeView` builds its context menu, so the overflow menu looks the
    // same as the context menu it repeats. They are put in whenever the commands change, not when the menu opens:
    // Avalonia's MenuFlyout raises Opening after it has created its popup, and items given to it then left the popup
    // open and empty.
    private void FillMoreFlyout()
    {
        if (MoreButton.Flyout is not MenuFlyout flyout) return;

        flyout.ItemsSource = (_document?.ToolbarActionItems ?? [])
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
