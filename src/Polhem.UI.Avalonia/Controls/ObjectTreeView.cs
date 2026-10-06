using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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
    /// <see cref="Control.ContextMenu"/> and fills it with the selected node's commands each time it opens.
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

        private ContextMenu? _commandMenu;

        /// <summary>
        /// Initializes a new instance of <see cref="ObjectTreeView"/>.
        /// </summary>
        public ObjectTreeView()
        {
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
