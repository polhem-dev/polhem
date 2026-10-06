using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Polhem.Definition.ObjectTree;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// Covers what can run without a UI thread. Subscribing to a node's children and evaluating the label binding
    /// both need Avalonia's dispatcher, which this assembly does not run, so those paths are left to the app.
    /// </summary>
    public class ObjectTreeViewTests
    {
        private static ObjectTreeNode CreateTree()
        {
            var root = new ObjectTreeNode(new object(), "root", isFolder: false);
            root.Children.Add(new ObjectTreeNode(new object(), "child", isFolder: false));
            return root;
        }

        [Fact]
        [DisplayName("Setting RootNode shows that single node as the tree's only item")]
        public void RootNode_Set_BecomesSingleItem()
        {
            var root = CreateTree();
            var tree = new ObjectTreeView { RootNode = root };

            Assert.Equal([root], tree.ItemsSource!.Cast<object>());
        }

        [Fact]
        [DisplayName("An item is a label bound to the node, with no icon when IconSelector is not set")]
        public void ItemTemplate_NoIconSelector_ShowsBoundLabelOnly()
        {
            var item = Assert.IsType<StackPanel>(new ObjectTreeView().ItemTemplate!.Build(CreateTree()));

            var label = Assert.IsType<TextBlock>(Assert.Single(item.Children));
            Assert.NotNull(BindingOperations.GetBindingExpressionBase(label, TextBlock.TextProperty));
        }

        [Fact]
        [DisplayName("An item shows the icon IconSelector returns before the label")]
        public void ItemTemplate_WithIconSelector_ShowsIcon()
        {
            var geometry = new RectangleGeometry();
            var tree = new ObjectTreeView { IconSelector = _ => geometry };

            var item = Assert.IsType<StackPanel>(tree.ItemTemplate!.Build(CreateTree()));

            Assert.Same(geometry, Assert.IsType<PathIcon>(item.Children[0]).Data);
            Assert.IsType<TextBlock>(item.Children[1]);
        }

        [Fact]
        [DisplayName("An IconSelector that returns null leaves the icon out")]
        public void ItemTemplate_IconSelectorReturnsNull_ShowsNoIcon()
        {
            var tree = new ObjectTreeView { IconSelector = _ => null };

            var item = Assert.IsType<StackPanel>(tree.ItemTemplate!.Build(CreateTree()));

            Assert.IsType<TextBlock>(Assert.Single(item.Children));
        }
    }
}
