using System.ComponentModel;
using Polhem.Definition.Attributes;
using Polhem.Definition.ObjectTree;

namespace Polhem.Definition.UnitTests.ObjectTree
{
    public class ObjectTreeNodeTests
    {
        [TreeNode("Holder")]
        private sealed class Holder
        {
            public ItemCollection Items { get; } = [];
        }

        [TreeNode("Items", false)]
        private sealed class ItemCollection : List<Item> { }

        [TreeNode("{0}", "Name")]
        private sealed class Item
        {
            public string Name { get; set; } = string.Empty;
        }

        [Fact]
        [DisplayName("Adding a node to Children sets its Parent, and removing it clears Parent")]
        public void Children_AddAndRemove_MaintainParent()
        {
            var parent = new ObjectTreeNode(new object(), "parent", isFolder: false);
            var child = new ObjectTreeNode(new object(), "child", isFolder: false);

            parent.Children.Add(child);
            Assert.Same(parent, child.Parent);

            parent.Children.Remove(child);
            Assert.Null(child.Parent);
        }

        [Fact]
        [DisplayName("Clearing Children clears the Parent of every child")]
        public void Children_Clear_ClearsParents()
        {
            var parent = new ObjectTreeNode(new object(), "parent", isFolder: false);
            var first = new ObjectTreeNode(new object(), "first", isFolder: false);
            var second = new ObjectTreeNode(new object(), "second", isFolder: false);
            parent.Children.Add(first);
            parent.Children.Add(second);

            parent.Children.Clear();

            Assert.Null(first.Parent);
            Assert.Null(second.Parent);
        }

        [Fact]
        [DisplayName("Replacing a child moves Parent from the old node to the new one")]
        public void Children_Replace_MovesParent()
        {
            var parent = new ObjectTreeNode(new object(), "parent", isFolder: false);
            var oldChild = new ObjectTreeNode(new object(), "old", isFolder: false);
            var newChild = new ObjectTreeNode(new object(), "new", isFolder: false);
            parent.Children.Add(oldChild);

            parent.Children[0] = newChild;

            Assert.Null(oldChild.Parent);
            Assert.Same(parent, newChild.Parent);
        }

        [Fact]
        [DisplayName("Adding a node that already has a parent throws InvalidOperationException")]
        public void Children_AddNodeWithParent_Throws()
        {
            var first = new ObjectTreeNode(new object(), "first", isFolder: false);
            var second = new ObjectTreeNode(new object(), "second", isFolder: false);
            var child = new ObjectTreeNode(new object(), "child", isFolder: false);
            first.Children.Add(child);

            Assert.Throws<InvalidOperationException>(() => second.Children.Add(child));
            Assert.Same(first, child.Parent);
        }

        [Fact]
        [DisplayName("Refresh recomputes the label of a built node and raises PropertyChanged for Label")]
        public void Refresh_BuiltNodeAfterEdit_UpdatesLabelAndNotifies()
        {
            var holder = new Holder();
            var item = new Item { Name = "before" };
            holder.Items.Add(item);
            var node = new ObjectTreeBuilder().Build(holder).Children[0];
            var changed = new List<string?>();
            node.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            item.Name = "after";
            node.Refresh();

            Assert.Equal("after", node.Label);
            Assert.Equal([nameof(ObjectTreeNode.Label)], changed);
        }

        [Fact]
        [DisplayName("Refresh raises no PropertyChanged when the label is unchanged")]
        public void Refresh_LabelUnchanged_DoesNotNotify()
        {
            var holder = new Holder();
            holder.Items.Add(new Item { Name = "same" });
            var node = new ObjectTreeBuilder().Build(holder).Children[0];
            var raised = false;
            node.PropertyChanged += (_, _) => raised = true;

            node.Refresh();

            Assert.False(raised);
        }

        [Fact]
        [DisplayName("Refresh keeps the fixed label of a node created with one")]
        public void Refresh_FixedLabelNode_KeepsLabel()
        {
            var node = new ObjectTreeNode(new Item { Name = "value" }, "Relation", isFolder: true);

            node.Refresh();

            Assert.Equal("Relation", node.Label);
        }
    }
}
