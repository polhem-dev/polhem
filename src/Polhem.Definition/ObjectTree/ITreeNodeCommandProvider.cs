namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// Supplies the commands a UI head offers for a tree node, for example in its context menu.
    /// </summary>
    /// <remarks>
    /// A consumer usually implements one provider per kind of document and dispatches on
    /// <see cref="ObjectTreeNode.Value"/> and <see cref="ObjectTreeNode.IsFolder"/>. The UI head asks again each
    /// time it shows the commands, so the list can depend on the current state.
    /// </remarks>
    public interface ITreeNodeCommandProvider
    {
        /// <summary>
        /// Gets the commands for <paramref name="node"/>, in the order they are shown.
        /// </summary>
        /// <param name="node">The node the commands act on.</param>
        /// <returns>The commands; an empty list when the node has none.</returns>
        IReadOnlyList<TreeNodeCommand> GetCommands(ObjectTreeNode node);
    }
}
