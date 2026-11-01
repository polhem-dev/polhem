namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// Keeps at most one editor open in a grid, for a UI package whose editors cost too much to render in every cell:
    /// a cell asks for the editor when it takes the focus, and the previous cell returns to text.
    /// </summary>
    public sealed class GridEditorHost
    {
        /// <summary>
        /// Gets the cell showing the editor, or <c>null</c>.
        /// </summary>
        public IGridEditorSlot? Active { get; private set; }

        /// <summary>
        /// Gives the editor to <paramref name="slot"/>, closing the one open elsewhere.
        /// </summary>
        /// <param name="slot">The cell that takes the editor.</param>
        public void Activate(IGridEditorSlot slot)
        {
            ArgumentNullException.ThrowIfNull(slot);
            if (ReferenceEquals(Active, slot)) return;
            var previous = Active;
            Active = slot;
            previous?.RefreshEditorState();
            slot.RefreshEditorState();
        }

        /// <summary>
        /// Closes the open editor, if any, as when the form enters View mode.
        /// </summary>
        public void Close()
        {
            var previous = Active;
            Active = null;
            previous?.RefreshEditorState();
        }

        /// <summary>
        /// Forgets <paramref name="slot"/> without rendering it, for a cell that is being disposed.
        /// </summary>
        /// <param name="slot">The cell that goes away.</param>
        public void Release(IGridEditorSlot slot)
        {
            if (ReferenceEquals(Active, slot)) Active = null;
        }
    }
}
