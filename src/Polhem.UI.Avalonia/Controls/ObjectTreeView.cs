using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Polhem.Definition.ObjectTree;

namespace Polhem.UI.Avalonia.Controls
{
    /// <summary>
    /// A <see cref="TreeView"/> that shows an <see cref="ObjectTreeNode"/> tree, as built by
    /// <see cref="ObjectTreeBuilder"/> from the <c>[TreeNode]</c> annotations.
    /// </summary>
    /// <remarks>
    /// Set <see cref="RootNode"/> to show a tree; the control replaces <see cref="ItemsControl.ItemsSource"/> with
    /// that single root. Each item shows the node's <see cref="ObjectTreeNode.Label"/>, after an icon when
    /// <see cref="IconSelector"/> returns one, and binds <see cref="TreeViewItem.IsExpanded"/> both ways to
    /// <see cref="ObjectTreeNode.IsExpanded"/>. <see cref="TreeView.SelectedItem"/> is the selected
    /// <see cref="ObjectTreeNode"/>. Labels are localized through <see cref="ObjectTreeOptions.LabelTranslator"/>
    /// when the tree is built. When <see cref="CommandProvider"/> is set, the control owns its
    /// <see cref="Control.ContextMenu"/> and fills it with the selected node's commands each time it opens. When
    /// <see cref="DragDropHandler"/> is set, nodes can be dragged and dropped before or after another node; the
    /// handler moves the objects and the control then moves the node in the tree.
    /// </remarks>
    public class ObjectTreeView : TreeView
    {
        /// <summary>
        /// Defines the <see cref="RootNode"/> property.
        /// </summary>
        public static readonly StyledProperty<ObjectTreeNode?> RootNodeProperty =
            AvaloniaProperty.Register<ObjectTreeView, ObjectTreeNode?>(nameof(RootNode));

        /// <summary>
        /// Defines the <see cref="IconSelector"/> property.
        /// </summary>
        public static readonly StyledProperty<Func<ObjectTreeNode, Geometry?>?> IconSelectorProperty =
            AvaloniaProperty.Register<ObjectTreeView, Func<ObjectTreeNode, Geometry?>?>(nameof(IconSelector));

        /// <summary>
        /// Defines the <see cref="CommandProvider"/> property.
        /// </summary>
        public static readonly StyledProperty<ITreeNodeCommandProvider?> CommandProviderProperty =
            AvaloniaProperty.Register<ObjectTreeView, ITreeNodeCommandProvider?>(nameof(CommandProvider));

        /// <summary>
        /// Defines the <see cref="DragDropHandler"/> property.
        /// </summary>
        public static readonly StyledProperty<ITreeNodeDragDropHandler?> DragDropHandlerProperty =
            AvaloniaProperty.Register<ObjectTreeView, ITreeNodeDragDropHandler?>(nameof(DragDropHandler));

        // The pointer must travel this far, in device-independent pixels, before a press becomes a drag, so a
        // click still just selects.
        private const double DragThreshold = 4;

        // Marks a drag that started in an ObjectTreeView. The node itself stays in _draggingNode: drags only move
        // nodes within the tree they started in, and Avalonia 12.0 has no in-process data format to carry it.
        private static readonly DataFormat<string> s_nodeFormat =
            DataFormat.CreateStringApplicationFormat("Polhem.ObjectTreeNode");

        private ContextMenu? _commandMenu;
        private PointerPressedEventArgs? _dragPress;
        private ObjectTreeNode? _dragNode;
        private ObjectTreeNode? _draggingNode;
        private Point _dragStart;
        private Border? _dropIndicator;
        private TreeViewItem? _dropIndicatorItem;

        /// <summary>
        /// Initializes a new instance of <see cref="ObjectTreeView"/>.
        /// </summary>
        public ObjectTreeView()
        {
            AddHandler(PointerPressedEvent, OnDragPointerPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, OnDragPointerMoved, RoutingStrategies.Tunnel);
            AddHandler(PointerReleasedEvent, (_, _) => ClearDragStart(), RoutingStrategies.Tunnel);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DragLeaveEvent, (_, _) => HideDropIndicator());
            AddHandler(DragDrop.DropEvent, OnDrop);
            ItemTemplate = CreateItemTemplate();
            Styles.Add(new Style(x => x.OfType<TreeViewItem>())
            {
                Setters =
                {
                    new Setter(TreeViewItem.IsExpandedProperty,
                        new ReflectionBinding(nameof(ObjectTreeNode.IsExpanded)) { Mode = BindingMode.TwoWay }),
                },
            });
        }

        /// <summary>
        /// Gets or sets the root of the tree to show, or <c>null</c> to show nothing.
        /// </summary>
        public ObjectTreeNode? RootNode
        {
            get { return GetValue(RootNodeProperty); }
            set { SetValue(RootNodeProperty, value); }
        }

        /// <summary>
        /// Gets or sets the function that picks the icon shown before a node's label; <c>null</c>, or a
        /// <c>null</c> result, shows no icon.
        /// </summary>
        public Func<ObjectTreeNode, Geometry?>? IconSelector
        {
            get { return GetValue(IconSelectorProperty); }
            set { SetValue(IconSelectorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the provider of the commands shown in the context menu for the selected node; <c>null</c>
        /// leaves <see cref="Control.ContextMenu"/> to the host.
        /// </summary>
        /// <remarks>
        /// The menu does not open when the selected node has no commands. A command's
        /// <see cref="TreeNodeCommand.IconKey"/> is looked up as a resource key from this control and shown when it
        /// resolves to a <see cref="Geometry"/>.
        /// </remarks>
        public ITreeNodeCommandProvider? CommandProvider
        {
            get { return GetValue(CommandProviderProperty); }
            set { SetValue(CommandProviderProperty, value); }
        }

        /// <summary>
        /// Gets or sets the handler that decides which nodes can be dragged and where they can be dropped;
        /// <c>null</c> turns dragging off.
        /// </summary>
        /// <remarks>
        /// A node is dropped before or after the node under the pointer, by which half of that node's row the
        /// pointer is in. After the handler's <see cref="ITreeNodeDragDropHandler.Drop"/>, the control moves the
        /// node to the same place among the target's siblings and selects it. A node cannot be dropped next to the
        /// root or inside its own subtree, whatever the handler says.
        /// </remarks>
        public ITreeNodeDragDropHandler? DragDropHandler
        {
            get { return GetValue(DragDropHandlerProperty); }
            set { SetValue(DragDropHandlerProperty, value); }
        }

        // NOTE: Without this override the control looks up a theme for its own type, and Semi and Fluent only ship
        // one for TreeView, so the whole control would be invisible (maintainers/gotchas/avalonia-controls.md #4).
        /// <inheritdoc/>
        protected override Type StyleKeyOverride => typeof(TreeView);

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == RootNodeProperty)
            {
                OnRootNodeChanged();
            }
            else if (change.Property == IconSelectorProperty)
            {
                ItemTemplate = CreateItemTemplate();
            }
            else if (change.Property == DragDropHandlerProperty)
            {
                DragDrop.SetAllowDrop(this, DragDropHandler != null);
            }
            else if (change.Property == CommandProviderProperty)
            {
                if (CommandProvider != null)
                    ContextMenu = _commandMenu ??= CreateCommandMenu();
                else if (ReferenceEquals(ContextMenu, _commandMenu))
                    ContextMenu = null;
            }
        }

        /// <summary>
        /// Builds the context menu entries for <paramref name="commands"/>: one item per command, with a separator
        /// before each command that begins a group.
        /// </summary>
        internal List<Control> BuildMenuItems(IReadOnlyList<TreeNodeCommand> commands)
        {
            var items = new List<Control>();
            foreach (var command in commands)
            {
                if (command.BeginsGroup && items.Count > 0)
                    items.Add(new Separator());
                var item = new MenuItem { Header = command.Label, IsEnabled = command.IsEnabled };
                if (command.IconKey is { } key
                    && this.TryFindResource(key, ActualThemeVariant, out var resource)
                    && resource is Geometry icon)
                {
                    item.Icon = new PathIcon { Data = icon, Width = 14, Height = 14 };
                }
                item.Click += (_, _) => command.Execute();
                items.Add(item);
            }
            return items;
        }

        private void OnRootNodeChanged()
        {
            var root = RootNode;
            if (root is null)
            {
                // A view that is re-parented, such as a tab's content, briefly loses its DataContext,
                // so a bound RootNode goes null and comes back within the same dispatcher turn. Clearing
                // ItemsSource at once would clear the selection, and a two-way SelectedItem binding still
                // attached to the view model would write that null back. Clear only if it stays null.
                Dispatcher.UIThread.Post(() =>
                {
                    if (RootNode is null)
                        ItemsSource = null;
                });
                return;
            }
            if (ItemsSource is ObjectTreeNode[] { Length: 1 } shown && ReferenceEquals(shown[0], root))
                return;
            ItemsSource = new[] { root };
        }

        /// <summary>
        /// Decides where <paramref name="node"/> lands when dropped at <paramref name="position"/> relative to
        /// <paramref name="target"/>: whether the drop is allowed, and if so the parent and the index among its
        /// children once <paramref name="node"/> has left its current place.
        /// </summary>
        internal static bool TryGetDropPlace(
            ITreeNodeDragDropHandler handler, ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position,
            out ObjectTreeNode parent, out int index)
        {
            parent = null!;
            index = -1;
            if (ReferenceEquals(node, target) || target.Parent is null || node.Parent is null) { return false; }
            for (var ancestor = target.Parent; ancestor != null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, node)) { return false; }
            if (!handler.CanDrop(node, target, position)) { return false; }

            parent = target.Parent;
            index = parent.Children.IndexOf(target) + (position == TreeNodeDropPosition.After ? 1 : 0);
            if (ReferenceEquals(node.Parent, parent) && parent.Children.IndexOf(node) < index)
                index--;
            return true;
        }

        /// <summary>
        /// Moves <paramref name="node"/> to <paramref name="index"/> among the children of <paramref name="parent"/>,
        /// where the index counts the children without <paramref name="node"/>.
        /// </summary>
        internal static void MoveNode(ObjectTreeNode node, ObjectTreeNode parent, int index)
        {
            if (ReferenceEquals(node.Parent, parent))
            {
                parent.Children.Move(parent.Children.IndexOf(node), index);
                return;
            }
            node.Parent?.Children.Remove(node);
            parent.Children.Insert(index, node);
        }

        private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            ClearDragStart();
            if (DragDropHandler is not { } handler || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { return; }
            if (NodeAt(e.Source) is not { } node || !handler.CanDrag(node)) { return; }
            _dragPress = e;
            _dragNode = node;
            _dragStart = e.GetPosition(this);
        }

        private async void OnDragPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragPress is not { } press || _dragNode is not { } node) { return; }
            var delta = e.GetPosition(this) - _dragStart;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) { return; }

            ClearDragStart();
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(s_nodeFormat, nameof(ObjectTreeNode)));
            _draggingNode = node;
            try
            {
                await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Move);
            }
            finally
            {
                _draggingNode = null;
                HideDropIndicator();
            }
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = DragDropEffects.None;
            if (TryGetDrop(e, out var item, out var position, out _, out _))
            {
                e.DragEffects = DragDropEffects.Move;
                ShowDropIndicator(item, position);
            }
            else
            {
                HideDropIndicator();
            }
            e.Handled = true;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            HideDropIndicator();
            e.Handled = true;
            if (!TryGetDrop(e, out var item, out var position, out var parent, out var index)) { return; }
            var node = _draggingNode!;
            var target = (ObjectTreeNode)item.DataContext!;
            DragDropHandler!.Drop(node, target, position);
            MoveNode(node, parent, index);
            SelectedItem = node;
        }

        private bool TryGetDrop(
            DragEventArgs e, out TreeViewItem item, out TreeNodeDropPosition position, out ObjectTreeNode parent, out int index)
        {
            item = null!;
            position = TreeNodeDropPosition.Before;
            parent = null!;
            index = -1;
            if (DragDropHandler is not { } handler || _draggingNode is not { } node || !e.DataTransfer.Contains(s_nodeFormat)) { return false; }
            if ((e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true) is not { DataContext: ObjectTreeNode target } found)
                return false;

            item = found;
            position = e.GetPosition(found).Y < HeaderHeight(found) / 2 ? TreeNodeDropPosition.Before : TreeNodeDropPosition.After;
            return TryGetDropPlace(handler, node, target, position, out parent, out index);
        }

        // An expanded item's bounds include its children; the header row ends where the children's presenter starts.
        private static double HeaderHeight(TreeViewItem item) =>
            item.IsExpanded && item.Presenter is { Bounds.Height: > 0 } children ? children.Bounds.Top : item.Bounds.Height;

        private void ShowDropIndicator(TreeViewItem item, TreeNodeDropPosition position)
        {
            _dropIndicator ??= new Border
            {
                Height = 2,
                IsHitTestVisible = false,
                Background = this.TryFindResource("SemiColorPrimary", ActualThemeVariant, out var brush) && brush is IBrush b
                    ? b
                    : Brushes.DodgerBlue,
            };
            _dropIndicator.VerticalAlignment = position == TreeNodeDropPosition.Before ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            if (ReferenceEquals(_dropIndicatorItem, item)) { return; }
            HideDropIndicator();
            AdornerLayer.SetAdorner(item, _dropIndicator);
            _dropIndicatorItem = item;
        }

        private void HideDropIndicator()
        {
            if (_dropIndicatorItem is null) { return; }
            AdornerLayer.SetAdorner(_dropIndicatorItem, null);
            _dropIndicatorItem = null;
        }

        private void ClearDragStart()
        {
            _dragPress = null;
            _dragNode = null;
        }

        private static ObjectTreeNode? NodeAt(object? source) =>
            (source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext as ObjectTreeNode;

        private ContextMenu CreateCommandMenu()
        {
            var menu = new ContextMenu();
            menu.Opening += OnCommandMenuOpening;
            return menu;
        }

        private void OnCommandMenuOpening(object? sender, CancelEventArgs e)
        {
            IReadOnlyList<TreeNodeCommand> commands = SelectedItem is ObjectTreeNode node && CommandProvider is { } provider
                ? provider.GetCommands(node)
                : [];
            if (commands.Count == 0)
            {
                e.Cancel = true;
                return;
            }
            ((ContextMenu)sender!).ItemsSource = BuildMenuItems(commands);
        }

        private FuncTreeDataTemplate<ObjectTreeNode> CreateItemTemplate()
        {
            var iconSelector = IconSelector;
            return new FuncTreeDataTemplate<ObjectTreeNode>(
                (node, _) => BuildItem(node, iconSelector),
                node => node.Children);
        }

        private static Control BuildItem(ObjectTreeNode node, Func<ObjectTreeNode, Geometry?>? iconSelector)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (iconSelector?.Invoke(node) is { } icon)
            {
                panel.Children.Add(new PathIcon
                {
                    Data = icon,
                    Width = 14,
                    Height = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }
            var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            label.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(ObjectTreeNode.Label)));
            panel.Children.Add(label);
            return panel;
        }
    }
}
