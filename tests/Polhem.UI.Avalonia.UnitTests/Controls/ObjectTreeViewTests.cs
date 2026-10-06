using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
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

        private sealed class FixedProvider(params TreeNodeCommand[] commands) : ITreeNodeCommandProvider
        {
            public IReadOnlyList<TreeNodeCommand> GetCommands(ObjectTreeNode node) => commands;
        }

        [Fact]
        [DisplayName("Setting CommandProvider gives the tree a context menu, and clearing it removes that menu")]
        public void CommandProvider_SetAndCleared_ManagesContextMenu()
        {
            var tree = new ObjectTreeView { CommandProvider = new FixedProvider() };
            Assert.NotNull(tree.ContextMenu);

            tree.CommandProvider = null;

            Assert.Null(tree.ContextMenu);
        }

        [Fact]
        [DisplayName("Clearing CommandProvider leaves a context menu the host set itself")]
        public void CommandProvider_Cleared_KeepsHostMenu()
        {
            var hostMenu = new ContextMenu();
            var tree = new ObjectTreeView { ContextMenu = hostMenu };

            tree.CommandProvider = null;

            Assert.Same(hostMenu, tree.ContextMenu);
        }

        [Fact]
        [DisplayName("BuildMenuItems makes one item per command and a separator before each later group")]
        public void BuildMenuItems_Groups_InsertSeparators()
        {
            var tree = new ObjectTreeView();

            var items = tree.BuildMenuItems(
            [
                new TreeNodeCommand("Add", () => { }) { BeginsGroup = true },
                new TreeNodeCommand("Rename", () => { }),
                new TreeNodeCommand("Delete", () => { }) { BeginsGroup = true, IsEnabled = false },
            ]);

            Assert.Equal(4, items.Count);
            Assert.Equal("Add", Assert.IsType<MenuItem>(items[0]).Header);
            Assert.Equal("Rename", Assert.IsType<MenuItem>(items[1]).Header);
            Assert.IsType<Separator>(items[2]);
            var delete = Assert.IsType<MenuItem>(items[3]);
            Assert.Equal("Delete", delete.Header);
            Assert.False(delete.IsEnabled);
        }

        [Fact]
        [DisplayName("Clicking a built menu item runs its command")]
        public void BuildMenuItems_Click_ExecutesCommand()
        {
            var runs = 0;
            var item = Assert.IsType<MenuItem>(Assert.Single(
                new ObjectTreeView().BuildMenuItems([new TreeNodeCommand("Add", () => runs++)])));

            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(1, runs);
        }
    }
}
