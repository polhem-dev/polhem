namespace Polhem.UI.Core.Editing
{
    /// <summary>
    /// The editing model of a grid, the same choice in every UI head. A UI-layer concern by design: the layout
    /// definitions stay framework-neutral, so the head that draws a grid decides how its rows are edited.
    /// </summary>
    public enum GridEditMode
    {
        /// <summary>
        /// Cells edit in place: each editable cell shows its field editor.
        /// </summary>
        InCell,

        /// <summary>
        /// Cells show text; a row is edited in an edit form built from the field editors, with commit and cancel.
        /// </summary>
        EditForm,
    }
}
