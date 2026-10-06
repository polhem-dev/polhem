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

        private sealed class AllowAll : ITreeNodeDragDropHandler
        {
            public bool CanDrag(ObjectTreeNode node) => true;
            public bool CanDrop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position) => true;
            public void Drop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position) { }
        }

        private static ObjectTreeNode CreateSiblings(out ObjectTreeNode a, out ObjectTreeNode b, out ObjectTreeNode c)
        {
            var root = new ObjectTreeNode(new object(), "root", isFolder: false);
            root.Children.Add(a = new ObjectTreeNode(new object(), "a", isFolder: false));
            root.Children.Add(b = new ObjectTreeNode(new object(), "b", isFolder: false));
            root.Children.Add(c = new ObjectTreeNode(new object(), "c", isFolder: false));
            return root;
        }

        [Theory]
        [InlineData(0, 2, TreeNodeDropPosition.After, "b,c,a")]
        [InlineData(0, 2, TreeNodeDropPosition.Before, "b,a,c")]
        [InlineData(2, 0, TreeNodeDropPosition.Before, "c,a,b")]
        [InlineData(2, 0, TreeNodeDropPosition.After, "a,c,b")]
        [InlineData(1, 0, TreeNodeDropPosition.After, "a,b,c")]
        [DisplayName("Dropping a node before or after a sibling moves it to that place")]
        public void DropPlace_AmongSiblings_MovesNode(int from, int onto, TreeNodeDropPosition position, string expected)
        {
            var root = CreateSiblings(out _, out _, out _);
            var node = root.Children[from];

            Assert.True(ObjectTreeView.TryGetDropPlace(new AllowAll(), node, root.Children[onto], position, out var parent, out var index));
            ObjectTreeView.MoveNode(node, parent, index);

            Assert.Equal(expected, string.Join(",", root.Children.Select(c => c.Label)));
            Assert.Same(root, node.Parent);
        }

        [Fact]
        [DisplayName("A node cannot be dropped on itself, next to the root, or inside its own subtree")]
        public void DropPlace_InvalidTargets_AreRejected()
        {
            var root = CreateSiblings(out var a, out var b, out _);
            var child = new ObjectTreeNode(new object(), "child", isFolder: false);
            a.Children.Add(child);

            Assert.False(ObjectTreeView.TryGetDropPlace(new AllowAll(), a, a, TreeNodeDropPosition.After, out _, out _));
            Assert.False(ObjectTreeView.TryGetDropPlace(new AllowAll(), b, root, TreeNodeDropPosition.After, out _, out _));
            Assert.False(ObjectTreeView.TryGetDropPlace(new AllowAll(), a, child, TreeNodeDropPosition.Before, out _, out _));
        }

        [Fact]
        [DisplayName("A drop the handler refuses is rejected")]
        public void DropPlace_HandlerRefuses_IsRejected()
        {
            var root = CreateSiblings(out var a, out var b, out _);

            Assert.False(ObjectTreeView.TryGetDropPlace(new RefuseAll(), a, b, TreeNodeDropPosition.After, out _, out _));
        }

        private sealed class RefuseAll : ITreeNodeDragDropHandler
        {
            public bool CanDrag(ObjectTreeNode node) => true;
            public bool CanDrop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position) => false;
            public void Drop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position) { }
        }
    }
}
