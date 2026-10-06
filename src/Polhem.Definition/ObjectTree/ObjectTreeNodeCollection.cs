using System.Collections.ObjectModel;

namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// The child nodes of an <see cref="ObjectTreeNode"/>. It keeps <see cref="ObjectTreeNode.Parent"/> in step with
    /// membership.
    /// </summary>
    public sealed class ObjectTreeNodeCollection : ObservableCollection<ObjectTreeNode>
    {
        private readonly ObjectTreeNode _owner;

        internal ObjectTreeNodeCollection(ObjectTreeNode owner)
        {
            _owner = owner;
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The node already belongs to another node.</exception>
        protected override void InsertItem(int index, ObjectTreeNode item)
        {
            Attach(item);
            base.InsertItem(index, item);
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The node already belongs to another node.</exception>
        protected override void SetItem(int index, ObjectTreeNode item)
        {
            var previous = this[index];
            if (ReferenceEquals(previous, item)) { return; }
            Attach(item);
            previous.Parent = null;
            base.SetItem(index, item);
        }

        /// <inheritdoc/>
        protected override void RemoveItem(int index)
        {
            this[index].Parent = null;
            base.RemoveItem(index);
        }

        /// <inheritdoc/>
        protected override void ClearItems()
        {
            foreach (var item in this)
                item.Parent = null;
            base.ClearItems();
        }

        private void Attach(ObjectTreeNode item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Parent != null)
                throw new InvalidOperationException("The node already belongs to another node; remove it from there first.");
            item.Parent = _owner;
        }
    }
}
