namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// A grid cell that can show an editor while it is the active one in a <see cref="GridEditorHost"/>.
    /// </summary>
    public interface IGridEditorSlot
    {
        /// <summary>
        /// Renders the cell again, after it gained or lost the editor.
        /// </summary>
        void RefreshEditorState();
    }
}
