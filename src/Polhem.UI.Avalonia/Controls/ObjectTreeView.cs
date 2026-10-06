using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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
    /// when the tree is built.
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
                var root = RootNode;
                ItemsSource = root is null ? null : new[] { root };
            }
            else if (change.Property == IconSelectorProperty)
            {
                ItemTemplate = CreateItemTemplate();
            }
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
