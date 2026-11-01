using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.DataObjects;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// The state and commands of the editing surface of the <see cref="Polhem.Web.Blazor.Controls.GridEditMode.EditForm"/> mode: one field editor per visible
    /// <see cref="LayoutColumn"/>, bound to a single row through the buffered edit protocol of
    /// <see cref="FormDataObject"/>. OK commits (<see cref="FormDataObject.CommitRowEdit"/>), Cancel restores
    /// (<see cref="FormDataObject.CancelRowEdit"/>). The counterpart of the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.RowEditPanel</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presentation-agnostic: a dialog hosts it in a modal overlay, but it can be placed anywhere. The session starts when the panel receives a row and ends on OK or Cancel; a panel removed
    /// from the page mid-session, or given another row, cancels the session first, so a half-committed edit
    /// never leaks.
    /// </para>
    /// <para>
    /// While the session runs the editors show proposed values, and refresh through
    /// <see cref="FormDataObject.RowEditFieldChanged"/> when something else writes the row, such as a
    /// recomputed computed field.
    /// </para>
    /// </remarks>
    public abstract class RowEditPanelBase : ComponentBase, IDisposable
    {
        private FormDataObject? _sessionObject;
        private DataRow? _row;

        /// <summary>Gets the row of the session, or <c>null</c> without one.</summary>
        protected DataRow? SessionRow => _row;

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

        /// <summary>Gets the style that lays the fields out in one or two columns.</summary>
        protected string FieldsStyle => Compact
            ? "display:grid;grid-template-columns:minmax(0,1fr);gap:8px"
            : "display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px 12px";

        /// <summary>Gets the localized text of <paramref name="key"/>.</summary>
        /// <param name="key">A <see cref="PolhemUIText"/> key.</param>
        /// <returns>The text.</returns>
        protected string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

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
        public void Dispose()
        {
            EndSession(commit: false);
            GC.SuppressFinalize(this);
        }

        /// <summary>Commits the session and reports it.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (!EndSession(commit: true)) return;
            await EditCommitted.InvokeAsync().ConfigureAwait(true);
        }

        /// <summary>Rolls the session back and reports it.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task CancelAsync(CancellationToken cancellationToken = default)
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

        /// <summary>Gets the columns to edit.</summary>
        /// <returns>The visible columns.</returns>
        protected IEnumerable<LayoutColumn> EnumerateVisibleColumns()
            => Layout?.Columns?.Where(c => c.Visible) ?? Enumerable.Empty<LayoutColumn>();

        /// <summary>Gets the id of a column's input, for its label.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The id.</returns>
        protected static string InputId(LayoutColumn column)
            => string.Create(CultureInfo.InvariantCulture, $"polhem-row-edit-{column.FieldName}");

        /// <summary>Builds the parameters of a column's editor.</summary>
        /// <param name="column">The column.</param>
        /// <returns>The parameters by name.</returns>
        protected Dictionary<string, object?> BuildEditorParameters(LayoutColumn column)
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
