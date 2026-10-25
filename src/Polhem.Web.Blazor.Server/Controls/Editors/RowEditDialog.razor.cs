using System.Data;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Modal presentation of <see cref="RowEditPanel"/>: edits one row over the page and reports the outcome
    /// through <see cref="EditCommitted"/> or <see cref="EditCancelled"/>. The counterpart of the Avalonia
    /// head's <c>RowEditDialog</c>; the host renders it while the row is being edited and removes it when
    /// either callback runs.
    /// </summary>
    /// <remarks>
    /// A click outside the card does not close it: unlike a lookup, an edit form holds typed input, which a
    /// stray click must not throw away. Only OK and Cancel end the session.
    /// </remarks>
    public sealed partial class RowEditDialog : ComponentBase
    {
        /// <summary>
        /// Gets or sets the data object that owns the row.
        /// </summary>
        [Parameter, EditorRequired]
        public FormDataObject? DataObject { get; set; }

        /// <summary>
        /// Gets or sets the grid layout whose columns drive the editors.
        /// </summary>
        [Parameter, EditorRequired]
        public LayoutGrid? Layout { get; set; }

        /// <summary>
        /// Gets or sets the row to edit.
        /// </summary>
        [Parameter, EditorRequired]
        public DataRow? Row { get; set; }

        /// <summary>
        /// Gets or sets whether the editors lay out in a single column (phone-sized screens).
        /// </summary>
        [Parameter]
        public bool Compact { get; set; }

        /// <summary>
        /// Invoked after OK commits the edit.
        /// </summary>
        [Parameter]
        public EventCallback EditCommitted { get; set; }

        /// <summary>
        /// Invoked after Cancel rolls the edit back.
        /// </summary>
        [Parameter]
        public EventCallback EditCancelled { get; set; }

        private string Title => Layout?.Caption ?? string.Empty;
    }
}
