namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// Decides which tree nodes a UI head lets the user drag, where they may be dropped, and applies a drop to the
    /// objects the nodes stand for.
    /// </summary>
    /// <remarks>
    /// The UI head asks <see cref="CanDrag"/> when a drag starts and <see cref="CanDrop"/> while the pointer moves,
    /// and calls <see cref="Drop"/> once on release. <see cref="Drop"/> changes the objects only; the UI head then
    /// moves the node in the tree to the same place, so the handler does not touch
    /// <see cref="ObjectTreeNode.Children"/>.
    /// </remarks>
    public interface ITreeNodeDragDropHandler
    {
        /// <summary>
        /// Gets whether <paramref name="node"/> can be dragged.
        /// </summary>
        /// <param name="node">The node under the pointer when the drag starts.</param>
        bool CanDrag(ObjectTreeNode node);

        /// <summary>
        /// Gets whether <paramref name="node"/> can be dropped at <paramref name="position"/> relative to
        /// <paramref name="target"/>.
        /// </summary>
        /// <param name="node">The dragged node.</param>
        /// <param name="target">The node under the pointer; never <paramref name="node"/> itself.</param>
        /// <param name="position">Before or after <paramref name="target"/>.</param>
        bool CanDrop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position);

        /// <summary>
        /// Moves the object <paramref name="node"/> stands for to <paramref name="position"/> relative to the
        /// object <paramref name="target"/> stands for. It is called only after <see cref="CanDrop"/> returned
        /// <c>true</c> for the same arguments.
        /// </summary>
        /// <param name="node">The dragged node.</param>
        /// <param name="target">The node it was dropped on.</param>
        /// <param name="position">Before or after <paramref name="target"/>.</param>
        void Drop(ObjectTreeNode node, ObjectTreeNode target, TreeNodeDropPosition position);
    }
}
