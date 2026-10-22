using System.ComponentModel;
using Avalonia.Controls;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// A row selected through the accessibility interface reaches <see cref="TreeView"/> as an item added to
    /// <see cref="TreeView.SelectedItems"/>; adding to the collection directly takes the same path.
    /// </summary>
    public class TreeViewExtensionsTests
    {
        private static TreeView CreateTree(out object first, out object second)
        {
            first = "first";
            second = "second";
            var tree = new TreeView { ItemsSource = new[] { first, second } };
            tree.KeepSelectionSingle();
            tree.SelectedItem = first;
            return tree;
        }

        [Fact]
        [DisplayName("An item added to the selection replaces the selected item")]
        public void KeepSelectionSingle_ItemAdded_BecomesSelectedItem()
        {
            var tree = CreateTree(out _, out var second);

            tree.SelectedItems.Add(second);

            Assert.Same(second, tree.SelectedItem);
            Assert.Equal([second], tree.SelectedItems.Cast<object>());
        }

        [Fact]
        [DisplayName("A tree that allows several selected items keeps all of them")]
        public void KeepSelectionSingle_MultipleMode_LeavesSelection()
        {
            var tree = CreateTree(out var first, out var second);
            tree.SelectionMode = SelectionMode.Multiple;

            tree.SelectedItems.Add(second);

            Assert.Same(first, tree.SelectedItem);
            Assert.Equal(2, tree.SelectedItems.Count);
        }

        [Fact]
        [DisplayName("Without the extension a tree keeps the old selected item, which is the behaviour it corrects")]
        public void TreeView_WithoutExtension_KeepsOldSelectedItem()
        {
            object first = "first", second = "second";
            var tree = new TreeView { ItemsSource = new[] { first, second } };
            tree.SelectedItem = first;

            tree.SelectedItems.Add(second);

            Assert.Same(first, tree.SelectedItem);
        }

        [Fact]
        [DisplayName("ObjectTreeView keeps a single selection")]
        public void ObjectTreeView_ItemAdded_BecomesSelectedItem()
        {
            object first = "first", second = "second";
            var tree = new ObjectTreeView { ItemsSource = new[] { first, second } };
            tree.SelectedItem = first;

            tree.SelectedItems.Add(second);

            Assert.Same(second, tree.SelectedItem);
        }
    }
}
