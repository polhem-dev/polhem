namespace Polhem.Web.Blazor.Server.Controls
{
    /// <summary>
    /// The editing model of a <see cref="GridControl"/>, the same choice as the Avalonia head's
    /// <c>GridEditMode</c>.
    /// </summary>
    public enum GridEditMode
    {
        /// <summary>
        /// Cells edit in place: each editable cell shows its field editor.
        /// </summary>
        InCell,

        /// <summary>
        /// Cells show text; a row is edited in <see cref="Polhem.Web.Blazor.Server.Controls.Editors.RowEditDialog"/>,
        /// opened by the toolbar's Edit button or a double-click.
        /// </summary>
        EditForm,
    }
}
