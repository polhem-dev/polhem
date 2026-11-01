namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The editing model of a <see cref="GridControlBase"/>, the same choice as the Avalonia head's
    /// <c>Polhem.UI.Avalonia.Controls.GridEditMode</c>.
    /// </summary>
    public enum GridEditMode
    {
        /// <summary>
        /// Cells edit in place: each editable cell shows its field editor.
        /// </summary>
        InCell,

        /// <summary>
        /// Cells show text; a row is edited in an edit form,
        /// opened by the toolbar's Edit button or a double-click.
        /// </summary>
        EditForm,
    }
}
