namespace Polhem.Definition.ObjectTree
{
    /// <summary>
    /// Where a dragged tree node lands relative to the node it is dropped on.
    /// </summary>
    public enum TreeNodeDropPosition
    {
        /// <summary>Directly before the target, under the same parent.</summary>
        Before,

        /// <summary>Directly after the target, under the same parent.</summary>
        After,
    }
}
