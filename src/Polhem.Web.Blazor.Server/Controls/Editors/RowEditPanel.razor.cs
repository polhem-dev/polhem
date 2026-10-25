using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// The editing surface of the <see cref="GridEditMode.EditForm"/> mode: one field editor per visible
    /// <see cref="LayoutColumn"/>, bound to a single row through the buffered edit protocol of
    /// <see cref="FormDataObject"/>. OK commits (<see cref="FormDataObject.CommitRowEdit"/>), Cancel restores
    /// (<see cref="FormDataObject.CancelRowEdit"/>). The counterpart of the Avalonia head's <c>RowEditPanel</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presentation-agnostic: <see cref="RowEditDialog"/> hosts it in a modal overlay, but it can be placed
    /// anywhere. The session starts when the panel receives a row and ends on OK or Cancel; a panel removed
    /// from the page mid-session, or given another row, cancels the session first, so a half-committed edit
    /// never leaks.
    /// </para>
    /// <para>
    /// While the session runs the editors show proposed values, and refresh through
    /// <see cref="FormDataObject.RowEditFieldChanged"/> when something else writes the row, such as a
    /// recomputed computed field.
    /// </para>
    /// </remarks>
    public sealed partial class RowEditPanel : ComponentBase, IDisposable
    {
        private FormDataObject? _sessionObject;
        private DataRow? _row;

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
        /// Gets or sets whether the editors lay out in a single column (phone-sized screens) instead of two.
        /// </summary>
        [Parameter]
        public bool Compact { get; set; }

        /// <summary>
        /// Invoked after OK commits the edit session.
        /// </summary>
        [Parameter]
        public EventCallback EditCommitted { get; set; }

        /// <summary>
        /// Invoked after Cancel rolls the edit session back.
        /// </summary>
        [Parameter]
        public EventCallback EditCancelled { get; set; }

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string FieldsStyle => Compact
            ? "display:grid;grid-template-columns:minmax(0,1fr);gap:8px"
            : "display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px 12px";

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            if (ReferenceEquals(Row, _row) && ReferenceEquals(DataObject, _sessionObject)) return;
            EndSession(commit: false);
            if (Row is null || DataObject is null) return;
            _sessionObject = DataObject;
            _row = Row;
            DataObject.BeginRowEdit(Row);
        }

        /// <inheritdoc />
        public void Dispose() => EndSession(commit: false);

        private async Task CommitAsync()
        {
            if (!EndSession(commit: true)) return;
            await EditCommitted.InvokeAsync().ConfigureAwait(true);
        }

        private async Task CancelAsync()
        {
            if (!EndSession(commit: false)) return;
            await EditCancelled.InvokeAsync().ConfigureAwait(true);
        }

        // Ends the current session, if any, and reports whether there was one.
        private bool EndSession(bool commit)
        {
            if (_sessionObject is not { } dataObject || _row is not { } row) return false;
            _sessionObject = null;
            _row = null;
            if (commit)
                dataObject.CommitRowEdit(row);
            else
                dataObject.CancelRowEdit(row);
            return true;
        }

        private IEnumerable<LayoutColumn> EnumerateVisibleColumns()
            => Layout?.Columns?.Where(c => c.Visible) ?? Enumerable.Empty<LayoutColumn>();

        private static string InputId(LayoutColumn column)
            => string.Create(CultureInfo.InvariantCulture, $"polhem-row-edit-{column.FieldName}");

        private Dictionary<string, object?> BuildEditorParameters(LayoutColumn column)
            => new(StringComparer.Ordinal)
            {
                [nameof(FieldEditorBase.FieldName)] = column.FieldName,
                [nameof(FieldEditorBase.LayoutField)] = column,
                [nameof(FieldEditorBase.Row)] = _row,
                [nameof(FieldEditorBase.DataObject)] = DataObject,
                [nameof(FieldEditorBase.Id)] = InputId(column),
            };
    }
}
