using System.Data;
using System.Globalization;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls
{
    /// <summary>
    /// A <see cref="GridControlBase"/> drawn as an HTML table. The counterpart of the Avalonia head's
    /// <c>Polhem.UI.Avalonia.Controls.GridControl</c>, with the same editing rules.
    /// </summary>
    /// <remarks>
    /// The edit form is <see cref="Polhem.Web.Blazor.Server.Controls.Editors.RowEditDialog"/>. The Avalonia grid needs
    /// click-to-swap editors and other workarounds for its native DataGrid's edit pipeline (ADR-021); an HTML table has
    /// no such pipeline, so this grid renders the editors directly. A required column's header carries the
    /// <c>polhem-grid__header--required</c> class.
    /// </remarks>
    public sealed partial class GridControl : GridControlBase
    {
        private const string AddIconGeometry =
            "M12 4a1 1 0 0 1 1 1v6h6a1 1 0 1 1 0 2h-6v6a1 1 0 1 1-2 0v-6H5a1 1 0 1 1 0-2h6V5a1 1 0 0 1 1-1Z";
        private const string EditIconGeometry =
            "M16.77 3.16a2.5 2.5 0 0 1 3.54 0l.53.53a2.5 2.5 0 0 1 0 3.54l-1.42 1.41-4.06-4.06 1.41-1.42Z" +
            "M14.3 5.64l4.06 4.06-9.53 9.53a2 2 0 0 1-.94.53l-3.6.9a.75.75 0 0 1-.91-.9l.9-3.61a2 2 0 0 1 .53-.94L14.3 5.64Z";
        private const string DeleteIconGeometry =
            "M10 2.5h4A1.5 1.5 0 0 1 15.5 4v.5H20a1 1 0 1 1 0 2h-.55l-.88 13.2A2.5 2.5 0 0 1 16.08 22H7.92" +
            "a2.5 2.5 0 0 1-2.49-2.3L4.55 6.5H4a1 1 0 0 1 0-2h4.5V4A1.5 1.5 0 0 1 10 2.5Z" +
            "M6.56 6.5l.86 12.97a.5.5 0 0 0 .5.46h8.16a.5.5 0 0 0 .5-.46l.86-12.97H6.56Z" +
            "M10 9.5a1 1 0 0 1 1 1v6a1 1 0 1 1-2 0v-6a1 1 0 0 1 1-1Zm4 0a1 1 0 0 1 1 1v6a1 1 0 1 1-2 0v-6a1 1 0 0 1 1-1Z";

        private string HeaderClass(LayoutColumn column)
            => ShowsRequiredCue(column)
                ? "polhem-grid__header polhem-grid__header--required"
                : "polhem-grid__header";

        private string RowClass(DataRow row)
            => IsSelected(row) ? "polhem-grid__row polhem-grid__row--selected" : "polhem-grid__row";

        private static string ColumnStyle(LayoutColumn column)
            => column.Width > 0 ? string.Create(CultureInfo.InvariantCulture, $"width:{column.Width}px") : string.Empty;
    }
}
