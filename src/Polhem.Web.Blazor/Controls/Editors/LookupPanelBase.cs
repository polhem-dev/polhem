using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// The state and commands of a lookup picker's selection surface: a search box, a list over the target form's lookup layout
    /// (see <see cref="FormSchema.GetLookupLayout"/>), and OK / Cancel buttons. Rows come from
    /// <see cref="IFormApiConnector.GetLookupAsync"/>, so the projection and the search are server-resolved.
    /// The counterpart of the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.LookupPanel</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presentation-agnostic: a lookup dialog hosts it in a modal overlay, but it can be placed anywhere. It loads the rows when it receives a connector, and again on each search.
    /// </para>
    /// <para>
    /// A click selects a row; a double-click, or OK, commits it through <see cref="OnCommitted"/>. A load
    /// failure is shown on the panel, which stays usable, so the user can search again.
    /// </para>
    /// </remarks>
    public abstract class LookupPanelBase : ComponentBase
    {
        private LayoutGrid? _layout;
        private DataTable? _rows;
        private Guid? _selectedRowId;
        private string? _error;
        private IFormApiConnector? _loadedConnector;

        /// <summary>Gets the layout of the list, or <c>null</c> without a schema.</summary>
        protected LayoutGrid? Layout => _layout;

        /// <summary>Gets the rows of the list, or <c>null</c> before they load.</summary>
        protected DataTable? Rows => _rows;

        /// <summary>Gets the id of the selected row, or <c>null</c>.</summary>
        protected Guid? SelectedRowId => _selectedRowId;

        /// <summary>Gets the message of a failed load, or <c>null</c>.</summary>
        protected string? Error => _error;

        /// <summary>Gets the notice that the server returned only part of the matches, or <c>null</c>.</summary>
        protected string? TruncationNotice => _truncationNotice;

        /// <summary>
        /// Gets or sets the target form's schema, which supplies the columns.
        /// </summary>
        [Parameter, EditorRequired]
        public FormSchema? Schema { get; set; }

        /// <summary>
        /// Gets or sets the connector for the target form, which supplies the rows.
        /// </summary>
        [Parameter, EditorRequired]
        public IFormApiConnector? Connector { get; set; }

        /// <summary>
        /// Invoked with the committed row (OK or a double-click).
        /// </summary>
        [Parameter]
        public EventCallback<DataRow> OnCommitted { get; set; }

        /// <summary>
        /// Invoked when the selection is cancelled.
        /// </summary>
        [Parameter]
        public EventCallback OnCancelled { get; set; }

        /// <summary>
        /// Gets or sets the search text submitted to the server on reload.
        /// </summary>
        public string SearchText { get; set; } = string.Empty;

        /// <summary>
        /// Gets the currently selected lookup row, or <c>null</c>.
        /// </summary>
        public DataRow? SelectedRow => _selectedRowId is { } id ? FindRow(id) : null;

        private string? _truncationNotice;
        private int _requestId;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets the localized text of <paramref name="key"/>.</summary>
        /// <param name="key">A <see cref="PolhemUIText"/> key.</param>
        /// <returns>The text.</returns>
        protected string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <summary>
        /// Reloads the lookup rows from the server using the current <see cref="SearchText"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that cancels the load. A cancelled load throws <see cref="OperationCanceledException"/> to the
        /// caller instead of being shown on the panel.
        /// </param>
        /// <returns>A task that completes when the rows are loaded or the failure is shown.</returns>
        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            var connector = Connector;
            if (connector is null) return;

            _error = null;
            var request = ++_requestId;
            try
            {
                var response = await connector.GetLookupAsync(SearchText, cancellationToken: cancellationToken)
                    .ConfigureAwait(true);
                // A slower answer to an earlier search must not replace the newer one's rows.
                if (request != _requestId) return;

                _rows = response.Table;
                _truncationNotice = response.Paging is { HasMore: true } paging
                    ? string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.LookupResultsTruncated), paging.PageSize)
                    : null;
                if (_selectedRowId is { } id && FindRow(id) is null)
                    _selectedRowId = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Boundary: surface the failure on the panel instead of breaking the picker,
                // as the Avalonia panel does.
                _error = ex.Message;
            }
        }

        /// <summary>Commits the selected row; a no-op without a selection.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (SelectedRow is { } row)
                await OnCommitted.InvokeAsync(row).ConfigureAwait(true);
        }

        /// <summary>Reports the cancellation.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task CancelAsync(CancellationToken cancellationToken = default) => OnCancelled.InvokeAsync();

        /// <summary>Reads the rows matching <see cref="SearchText"/>.</summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected Task SearchAsync(CancellationToken cancellationToken = default) => ReloadAsync(cancellationToken);

        /// <inheritdoc />
        protected override async Task OnParametersSetAsync()
        {
            _layout = Schema?.GetLookupLayout();
            if (Connector is not null && !ReferenceEquals(Connector, _loadedConnector))
            {
                _loadedConnector = Connector;
                _selectedRowId = null;
                await ReloadAsync().ConfigureAwait(true);
            }
        }

        /// <summary>Selects a row.</summary>
        /// <param name="rowId">The row's id.</param>
        protected void Select(Guid rowId) => _selectedRowId = rowId;

        /// <summary>Selects a row and commits it, as a double-click does.</summary>
        /// <param name="rowId">The row's id.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The operation.</returns>
        protected async Task ActivateAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            _selectedRowId = rowId;
            await CommitAsync(cancellationToken).ConfigureAwait(true);
        }

        private DataRow? FindRow(Guid rowId)
            => _rows?.Rows.Cast<DataRow>()
                .FirstOrDefault(r => FormDataGuard.TryGetRowId(r, out var id) && id == rowId);
    }
}
