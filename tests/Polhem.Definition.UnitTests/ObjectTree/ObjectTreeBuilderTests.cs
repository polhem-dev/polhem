using System.ComponentModel;
using Polhem.Definition.Attributes;
using Polhem.Definition.ObjectTree;

namespace Polhem.Definition.UnitTests.ObjectTree
{
    public class ObjectTreeBuilderTests
    {
        [TreeNode("Root")]
        private sealed class RootObject
        {
            public string Name { get; set; } = "root";
            public FolderCollection Folder { get; } = [];
            public FlatCollection Flat { get; } = [];
            public ChildObject? Child { get; set; }
            [TreeNodeIgnore]
            public ChildObject? Ignored { get; set; }
            [Browsable(false)]
            public ChildObject? Hidden { get; set; }
            public PlainObject? Plain { get; set; }
            public object? Back { get; set; }
        }

        [TreeNode("Folder", true)]
        private sealed class FolderCollection : List<object> { }

        [TreeNode("Flat", false)]
        private sealed class FlatCollection : List<object> { }

        [TreeNode("Item {0}", "Name")]
        private sealed class ItemObject
        {
            public string Name { get; set; } = string.Empty;
            public object? Next { get; set; }
        }

        [TreeNode("Child")]
        private sealed class ChildObject { }

        private sealed class PlainObject
        {
            public override string ToString() => "plain";
        }

        [TreeNode("Chain")]
        private sealed class ChainObject
        {
            public ChainObject? Next { get; set; }
        }

        private static ChainObject BuildChain(int length)
        {
            var head = new ChainObject();
            var current = head;
            for (int i = 1; i < length; i++)
            {
                current.Next = new ChainObject();
                current = current.Next;
            }
            return head;
        }

        private static int Depth(ObjectTreeNode node)
        {
            return node.Children.Count == 0 ? 0 : 1 + node.Children.Max(Depth);
        }

        [Fact]
        [DisplayName("Build gives the root a node even when its type has no TreeNode attribute")]
        public void Build_UnannotatedRoot_ReturnsRootNode()
        {
            var root = new PlainObject();

            var node = new ObjectTreeBuilder().Build(root);

            Assert.Same(root, node.Value);
            Assert.Equal("plain", node.Label);
            Assert.Null(node.Parent);
            Assert.Empty(node.Children);
        }

        [Fact]
        [DisplayName("A collection whose attribute sets CollectionFolder becomes a folder node holding its items")]
        public void Build_CollectionFolderTrue_CreatesFolderNode()
        {
            var root = new RootObject();
            root.Folder.Add(new ItemObject { Name = "a" });
            root.Folder.Add(new ItemObject { Name = "b" });

            var node = new ObjectTreeBuilder().Build(root);

            var folder = Assert.Single(node.Children);
            Assert.True(folder.IsFolder);
            Assert.Same(root.Folder, folder.Value);
            Assert.Equal("Folder", folder.Label);
            Assert.Equal(["Item a", "Item b"], folder.Children.Select(c => c.Label));
        }

        [Fact]
        [DisplayName("A collection whose attribute clears CollectionFolder puts its items directly under the parent")]
        public void Build_CollectionFolderFalse_AttachesItemsToParent()
        {
            var root = new RootObject();
            root.Flat.Add(new ItemObject { Name = "x" });

            var node = new ObjectTreeBuilder().Build(root);

            var item = Assert.Single(node.Children, c => !c.IsFolder);
            Assert.Same(root.Flat[0], item.Value);
            Assert.Same(node, item.Parent);
        }

        [Fact]
        [DisplayName("An empty folder collection still gets a folder node so items can be added to it")]
        public void Build_EmptyFolderCollection_KeepsFolderNode()
        {
            var node = new ObjectTreeBuilder().Build(new RootObject());

            var folder = Assert.Single(node.Children);
            Assert.True(folder.IsFolder);
            Assert.Empty(folder.Children);
        }

        [Fact]
        [DisplayName("Every item of an annotated collection becomes a node, annotated or not")]
        public void Build_UnannotatedItemInAnnotatedCollection_GetsNode()
        {
            var root = new RootObject();
            root.Flat.Add(new PlainObject());

            var node = new ObjectTreeBuilder().Build(root);

            Assert.Equal("plain", Assert.Single(node.Children, c => !c.IsFolder).Label);
        }

        [Fact]
        [DisplayName("An object property whose value type carries TreeNode becomes a child node")]
        public void Build_AnnotatedObjectProperty_CreatesChildNode()
        {
            var root = new RootObject { Child = new ChildObject() };

            var node = new ObjectTreeBuilder().Build(root);

            Assert.Contains(node.Children, c => ReferenceEquals(c.Value, root.Child));
        }

        [Fact]
        [DisplayName("Properties marked TreeNodeIgnore and values without TreeNode are skipped")]
        public void Build_IgnoredAndUnannotated_AreSkipped()
        {
            var root = new RootObject
            {
                Ignored = new ChildObject(),
                Plain = new PlainObject(),
            };

            var node = new ObjectTreeBuilder().Build(root);

            Assert.DoesNotContain(node.Children, c => c.Value is ChildObject or PlainObject);
        }

        [Fact]
        [DisplayName("A Browsable(false) property is still followed, because property grids and trees hide different things")]
        public void Build_BrowsableFalseProperty_IsFollowed()
        {
            var root = new RootObject { Hidden = new ChildObject() };

            var node = new ObjectTreeBuilder().Build(root);

            Assert.Contains(node.Children, c => ReferenceEquals(c.Value, root.Hidden));
        }

        [Fact]
        [DisplayName("A property that points back to an ancestor does not recurse")]
        public void Build_BackReferenceToAncestor_DoesNotRecurse()
        {
            var root = new RootObject();
            var item = new ItemObject { Name = "loop", Next = root };
            root.Flat.Add(item);
            root.Back = root;

            var node = new ObjectTreeBuilder().Build(root);

            var itemNode = Assert.Single(node.Children, c => !c.IsFolder);
            Assert.Empty(itemNode.Children);
        }

        [Fact]
        [DisplayName("An object reachable along two paths gets only one node")]
        public void Build_SharedReference_GetsOneNode()
        {
            var shared = new ItemObject { Name = "shared" };
            var root = new RootObject();
            root.Flat.Add(shared);
            root.Folder.Add(shared);

            var node = new ObjectTreeBuilder().Build(root);

            var all = node.Children.SelectMany(c => c.Children.Prepend(c));
            Assert.Single(all, c => ReferenceEquals(c.Value, shared));
        }

        [Fact]
        [DisplayName("Build stops at MaxDepth instead of throwing")]
        public void Build_ChainDeeperThanMaxDepth_StopsAtMaxDepth()
        {
            var options = new ObjectTreeOptions { MaxDepth = 3 };

            var node = new ObjectTreeBuilder(options).Build(BuildChain(10));

            Assert.Equal(3, Depth(node));
        }

        [Fact]
        [DisplayName("A MaxDepth of zero builds the root alone")]
        public void Build_MaxDepthZero_ReturnsRootOnly()
        {
            var options = new ObjectTreeOptions { MaxDepth = 0 };

            var node = new ObjectTreeBuilder(options).Build(BuildChain(5));

            Assert.Empty(node.Children);
        }

        [Fact]
        [DisplayName("The default MaxDepth stops a very deep object graph without throwing")]
        public void Build_VeryDeepChainWithDefaults_StopsAtDefaultDepth()
        {
            var options = new ObjectTreeOptions();

            var node = new ObjectTreeBuilder(options).Build(BuildChain(10_000));

            Assert.Equal(options.MaxDepth, Depth(node));
        }

        [Fact]
        [DisplayName("Setting a negative MaxDepth throws ArgumentOutOfRangeException")]
        public void MaxDepth_Negative_Throws()
        {
            var options = new ObjectTreeOptions();

            Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxDepth = -1);
        }

        [Fact]
        [DisplayName("ExpandDepth expands the built nodes above that level and leaves the rest collapsed")]
        public void Build_ExpandDepth_ExpandsUpperLevels()
        {
            var options = new ObjectTreeOptions { ExpandDepth = 2 };

            var root = new ObjectTreeBuilder(options).Build(BuildChain(4));

            Assert.True(root.IsExpanded);
            Assert.True(root.Children[0].IsExpanded);
            Assert.False(root.Children[0].Children[0].IsExpanded);
        }

        [Fact]
        [DisplayName("By default no built node starts expanded")]
        public void Build_DefaultExpandDepth_ExpandsNothing()
        {
            var root = new ObjectTreeBuilder().Build(BuildChain(2));

            Assert.False(root.IsExpanded);
            Assert.False(root.Children[0].IsExpanded);
        }

        [Fact]
        [DisplayName("Setting a negative ExpandDepth throws ArgumentOutOfRangeException")]
        public void ExpandDepth_Negative_Throws()
        {
            var options = new ObjectTreeOptions();

            Assert.Throws<ArgumentOutOfRangeException>(() => options.ExpandDepth = -1);
        }

        [Fact]
        [DisplayName("PropertyFilter returning false leaves the property out of the tree")]
        public void Build_PropertyFilterRejects_SkipsProperty()
        {
            var root = new RootObject();
            root.Flat.Add(new ItemObject { Name = "x" });
            var options = new ObjectTreeOptions
            {
                PropertyFilter = (_, property) => property.Name != nameof(RootObject.Folder),
            };

            var node = new ObjectTreeBuilder(options).Build(root);

            Assert.DoesNotContain(node.Children, c => c.IsFolder);
            Assert.Single(node.Children);
        }

        [Fact]
        [DisplayName("NodeBuilt can add a child node built with the builder it receives")]
        public void Build_NodeBuiltAddsChild_ChildIsAttached()
        {
            var extra = new ItemObject { Name = "extra" };
            var options = new ObjectTreeOptions
            {
                NodeBuilt = (node, builder) =>
                {
                    if (node.Value is RootObject)
                        node.Children.Add(builder.Build(extra));
                },
            };

            var root = new ObjectTreeBuilder(options).Build(new RootObject());

            var added = Assert.Single(root.Children, c => ReferenceEquals(c.Value, extra));
            Assert.Same(root, added.Parent);
            Assert.Equal("Item extra", added.Label);
        }

        [Fact]
        [DisplayName("NodeBuilt runs for folder nodes as well as object nodes")]
        public void Build_NodeBuilt_RunsForFolderNodes()
        {
            var seen = new List<ObjectTreeNode>();
            var options = new ObjectTreeOptions { NodeBuilt = (node, _) => seen.Add(node) };

            new ObjectTreeBuilder(options).Build(new RootObject());

            Assert.Contains(seen, n => n.IsFolder);
            Assert.Contains(seen, n => n.Value is RootObject);
        }

        [Fact]
        [DisplayName("LabelTranslator translates the format but not the property values put into it")]
        public void Build_LabelTranslator_TranslatesFormatOnly()
        {
            var root = new RootObject();
            root.Flat.Add(new ItemObject { Name = "Item" });
            var options = new ObjectTreeOptions
            {
                LabelTranslator = format => format.Replace("Item", "Entry", StringComparison.Ordinal),
            };

            var node = new ObjectTreeBuilder(options).Build(root);

            Assert.Equal("Entry Item", Assert.Single(node.Children, c => !c.IsFolder).Label);
        }
    }
}
