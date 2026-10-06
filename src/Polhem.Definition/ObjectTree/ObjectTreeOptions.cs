using System.ComponentModel;

namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// Options for <see cref="ObjectTreeBuilder"/>.
    /// </summary>
    public sealed class ObjectTreeOptions
    {
        private int _maxDepth = 32;
        private int _expandDepth;

        /// <summary>
        /// Gets or sets the deepest level that gets nodes; the root is level 0 and a folder node counts as a level.
        /// Nodes below it are not created. The default is 32.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public int MaxDepth
        {
            get { return _maxDepth; }
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                _maxDepth = value;
            }
        }

        /// <summary>
        /// Gets or sets how many levels start expanded: a built node at a level below this value has
        /// <see cref="ObjectTreeNode.IsExpanded"/> set. The root is level 0 and a folder node counts as a level, so
        /// 1 expands the root alone. The default is 0, which expands nothing.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public int ExpandDepth
        {
            get { return _expandDepth; }
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                _expandDepth = value;
            }
        }

        /// <summary>
        /// Gets or sets a filter that decides whether a property of an object is followed; return <c>false</c> to
        /// leave it out of the tree. It is called with the object and the property, after the built-in rules have
        /// already kept the property. <c>null</c> follows every property the built-in rules keep.
        /// </summary>
        public Func<object, PropertyDescriptor, bool>? PropertyFilter { get; set; }

        /// <summary>
        /// Gets or sets a callback that runs after a node and its children are built, before the node is attached
        /// to its parent. It may add, remove or reorder children, for example to add grouping nodes the
        /// annotations cannot express. <c>null</c> runs nothing.
        /// </summary>
        /// <remarks>
        /// The callback receives the builder, so it can build child nodes for objects of its own with
        /// <see cref="ObjectTreeBuilder.Build"/>. Each such call starts a separate walk: the objects already in the
        /// tree being built are not known to it.
        /// </remarks>
        public Action<ObjectTreeNode, ObjectTreeBuilder>? NodeBuilt { get; set; }

        /// <summary>
        /// Gets or sets the translator applied to each <see cref="Attributes.TreeNodeAttribute.DisplayFormat"/>
        /// before property values are put into it; <c>null</c> shows the labels as written.
        /// </summary>
        public Func<string, string>? LabelTranslator { get; set; }
    }
}
