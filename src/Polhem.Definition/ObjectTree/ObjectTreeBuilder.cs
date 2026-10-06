using System.Collections;
using System.ComponentModel;
using Polhem.Core.Collections;
using Polhem.Definition.Attributes;

namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// Builds an <see cref="ObjectTreeNode"/> tree from an object graph, driven by <see cref="TreeNodeAttribute"/>
    /// and <see cref="TreeNodeIgnoreAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A property is followed when it carries no <see cref="TreeNodeIgnoreAttribute"/>, is not of a value type or
    /// <see cref="string"/>, is not declared by one of the collection base types (<see cref="CollectionItem"/>,
    /// <see cref="KeyCollectionItem"/>, <see cref="CollectionBase{T}"/>, <see cref="KeyCollectionBase{T}"/>, whose
    /// own properties are back references and tags), passes <see cref="ObjectTreeOptions.PropertyFilter"/>, and its
    /// value's type carries <see cref="TreeNodeAttribute"/>. <see cref="BrowsableAttribute"/> plays no part: the
    /// collection properties of the definition types are hidden from property grids but belong in the tree. A value that is a collection contributes its items: inside a
    /// folder node when the attribute sets <see cref="TreeNodeAttribute.CollectionFolder"/>, directly under the
    /// parent otherwise. Every item of such a collection becomes a node, annotated or not. Any other value becomes
    /// a node of its own.
    /// </para>
    /// <para>
    /// The annotations do not prevent cycles on their own, so the walk keeps the objects it has already placed and
    /// gives each object at most one node, and it stops at <see cref="ObjectTreeOptions.MaxDepth"/>.
    /// </para>
    /// <para>
    /// Reading a property runs its getter. The collection getters of the definition types create an empty
    /// collection on first read, which does not change how the object serializes but is still a write, so build
    /// from an instance you own (a loaded file or a clone), not from a cached one.
    /// </para>
    /// </remarks>
    public sealed class ObjectTreeBuilder
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ObjectTreeBuilder"/>.
        /// </summary>
        /// <param name="options">The options; <c>null</c> uses the defaults.</param>
        public ObjectTreeBuilder(ObjectTreeOptions? options = null)
        {
            Options = options ?? new ObjectTreeOptions();
        }

        /// <summary>
        /// Gets the options the builder uses.
        /// </summary>
        public ObjectTreeOptions Options { get; }

        /// <summary>
        /// Builds the tree whose root stands for <paramref name="root"/>.
        /// </summary>
        /// <param name="root">The object at the root. It does not need to carry <see cref="TreeNodeAttribute"/>.</param>
        /// <returns>The root node, with no parent.</returns>
        public ObjectTreeNode Build(object root)
        {
            ArgumentNullException.ThrowIfNull(root);
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance) { root };
            return BuildObjectNode(root, 0, visited);
        }

        private ObjectTreeNode BuildObjectNode(object value, int depth, HashSet<object> visited)
        {
            var node = CreateNode(value, isFolder: false);
            if (depth < Options.MaxDepth)
                AddPropertyChildren(node, depth + 1, visited);
            Options.NodeBuilt?.Invoke(node, this);
            return node;
        }

        private void AddPropertyChildren(ObjectTreeNode node, int depth, HashSet<object> visited)
        {
            var owner = node.Value;
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(owner))
            {
                if (!IsFollowed(owner, property)) { continue; }

                var value = property.GetValue(owner);
                if (value == null || !visited.Add(value)) { continue; }

                var attribute = GetTreeNodeAttribute(value);
                if (attribute == null) { continue; }

                if (value is IEnumerable items)
                {
                    if (attribute.CollectionFolder)
                        node.Children.Add(BuildFolderNode(value, items, depth, visited));
                    else
                        AddItemNodes(node, items, depth, visited);
                }
                else
                {
                    node.Children.Add(BuildObjectNode(value, depth, visited));
                }
            }
        }

        private ObjectTreeNode BuildFolderNode(object collection, IEnumerable items, int depth, HashSet<object> visited)
        {
            var folder = CreateNode(collection, isFolder: true);
            if (depth < Options.MaxDepth)
                AddItemNodes(folder, items, depth + 1, visited);
            Options.NodeBuilt?.Invoke(folder, this);
            return folder;
        }

        private void AddItemNodes(ObjectTreeNode parent, IEnumerable items, int depth, HashSet<object> visited)
        {
            foreach (var item in items)
            {
                if (item == null || !visited.Add(item)) { continue; }
                parent.Children.Add(BuildObjectNode(item, depth, visited));
            }
        }

        private bool IsFollowed(object owner, PropertyDescriptor property)
        {
            if (property.PropertyType.IsValueType || property.PropertyType == typeof(string)) { return false; }
            if (property.Attributes[typeof(TreeNodeIgnoreAttribute)] != null) { return false; }
            if (IsDeclaredByCollectionBase(property.ComponentType)) { return false; }
            return Options.PropertyFilter?.Invoke(owner, property) ?? true;
        }

        private ObjectTreeNode CreateNode(object value, bool isFolder)
        {
            var translator = Options.LabelTranslator;
            Func<object, string> labelProvider = v => TreeNodeAttribute.GetDisplayText(v, translator);
            return new ObjectTreeNode(value, labelProvider(value), isFolder, labelProvider);
        }

        private static bool IsDeclaredByCollectionBase(Type declaringType)
        {
            if (declaringType == typeof(CollectionItem) || declaringType == typeof(KeyCollectionItem)) { return true; }
            if (!declaringType.IsGenericType) { return false; }
            var definition = declaringType.GetGenericTypeDefinition();
            return definition == typeof(CollectionBase<>) || definition == typeof(KeyCollectionBase<>);
        }

        private static TreeNodeAttribute? GetTreeNodeAttribute(object value)
        {
            return TypeDescriptor.GetAttributes(value)[typeof(TreeNodeAttribute)] as TreeNodeAttribute;
        }
    }
}
