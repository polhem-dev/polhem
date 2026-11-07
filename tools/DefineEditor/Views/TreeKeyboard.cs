using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Polhem.DefineEditor.ViewModels;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// The keys of an editor's tree: Delete or Backspace deletes the selected node, Cmd/Ctrl+D duplicates it,
/// Alt+Up and Alt+Down move it among its siblings, and Enter or F2 moves the focus to the first value of the property
/// grid, from where Escape brings it back to the tree.
/// </summary>
/// <remarks>
/// The keys are read only while the tree itself has the focus, so typing in the property grid never deletes a node.
/// The tooltips write them as <see cref="TreeShortcuts"/> does.
/// </remarks>
public static class TreeKeyboard
{
    /// <summary>Defines the attached property that turns the keys on for a tree.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ObjectTreeView, bool>("IsEnabled", typeof(TreeKeyboard));

    // The tree of each editor view, for the view's Escape handler.
    private static readonly ConditionalWeakTable<UserControl, ObjectTreeView> s_trees = [];

    static TreeKeyboard()
    {
        IsEnabledProperty.Changed.AddClassHandler<ObjectTreeView>(OnIsEnabledChanged);
    }

    /// <summary>Gets whether the keys are on for <paramref name="tree"/>.</summary>
    public static bool GetIsEnabled(ObjectTreeView tree) => tree.GetValue(IsEnabledProperty);

    /// <summary>Turns the keys on or off for <paramref name="tree"/>.</summary>
    public static void SetIsEnabled(ObjectTreeView tree, bool value) => tree.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(ObjectTreeView tree, AvaloniaPropertyChangedEventArgs e)
    {
        // Tunnel, so Alt+Up and Alt+Down reach this handler before the tree's own arrow-key navigation.
        tree.RemoveHandler(InputElement.KeyDownEvent, OnTreeKeyDown);
        tree.AttachedToVisualTree -= OnTreeAttached;
        tree.DetachedFromVisualTree -= OnTreeDetached;
        if (e.NewValue is not true) return;

        tree.AddHandler(InputElement.KeyDownEvent, OnTreeKeyDown, RoutingStrategies.Tunnel);
        tree.AttachedToVisualTree += OnTreeAttached;
        tree.DetachedFromVisualTree += OnTreeDetached;
        if (tree.IsAttachedToVisualTree())
            HookPropertyGrid(tree, attach: true);
    }

    private static void OnTreeAttached(object? sender, VisualTreeAttachmentEventArgs e) =>
        HookPropertyGrid((ObjectTreeView)sender!, attach: true);

    private static void OnTreeDetached(object? sender, VisualTreeAttachmentEventArgs e) =>
        HookPropertyGrid((ObjectTreeView)sender!, attach: false);

    private static void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ObjectTreeView tree || tree.DataContext is not ObjectTreeDocumentViewModelBase document)
            return;

        var commandModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        ICommand? command = (e.Key, e.KeyModifiers) switch
        {
            (Key.Delete or Key.Back, KeyModifiers.None) => document.DeleteCommand,
            (Key.D, var modifiers) when modifiers == commandModifier => document.DuplicateCommand,
            (Key.Up, KeyModifiers.Alt) => document.MoveUpCommand,
            (Key.Down, KeyModifiers.Alt) => document.MoveDownCommand,
            _ => null,
        };
        if (command is not null)
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
                // Moving a node replaces its container and duplicating selects a new one, so the focus would stay
                // behind and the next key would miss the tree. Hand it to the selected node once the tree has caught up.
                Dispatcher.UIThread.Post(() => FocusSelectedNode(tree), DispatcherPriority.Loaded);
            }
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.F2 && e.KeyModifiers == KeyModifiers.None && FocusPropertyGrid(tree))
            e.Handled = true;
    }

    // The property grid that edits the tree's selection: the one in the same editor view.
    private static PropertyGridControl? FindPropertyGrid(ObjectTreeView tree) =>
        tree.FindAncestorOfType<UserControl>()?.GetVisualDescendants().OfType<PropertyGridControl>().FirstOrDefault();

    private static bool FocusPropertyGrid(ObjectTreeView tree)
    {
        var editor = FindPropertyGrid(tree)?.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(c => c is TextBox or ComboBox or NumericUpDown or CheckBox
                && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable);
        return editor?.Focus(NavigationMethod.Tab) ?? false;
    }

    // Escape in the property grid hands the focus back to the tree. The grid writes a value when its editor loses the
    // focus, so the edit is kept. The handler is added to the editor view, the parent the grid and the tree share,
    // because the grid is created by its view while the tree's attached property is set.
    private static void HookPropertyGrid(ObjectTreeView tree, bool attach)
    {
        if (tree.FindAncestorOfType<UserControl>() is not { } view) return;
        view.RemoveHandler(InputElement.KeyDownEvent, OnViewKeyDown);
        if (attach)
        {
            view.AddHandler(InputElement.KeyDownEvent, OnViewKeyDown, RoutingStrategies.Bubble);
            s_trees.AddOrUpdate(view, tree);
        }
        else
        {
            s_trees.Remove(view);
        }
    }

    private static void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None
            || sender is not UserControl view || !s_trees.TryGetValue(view, out var tree)
            || e.Source is not Visual source || source.FindAncestorOfType<PropertyGridControl>(includeSelf: true) is null)
            return;

        if (FocusSelectedNode(tree))
            e.Handled = true;
    }

    private static bool FocusSelectedNode(ObjectTreeView tree)
    {
        var selected = tree.SelectedItem is { } item ? tree.TreeContainerFromItem(item) : null;
        return selected?.Focus(NavigationMethod.Tab) ?? tree.Focus(NavigationMethod.Tab);
    }
}
