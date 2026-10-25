using System.Data;
using Microsoft.AspNetCore.Components;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// The selection surface of a lookup picker: a search box, a list over the target form's lookup layout
    /// (see <see cref="FormSchema.GetLookupLayout"/>), and OK / Cancel buttons. Rows come from
    /// <see cref="IFormApiConnector.GetLookupAsync"/>, so the projection and the search are server-resolved.
    /// The counterpart of the Avalonia head's <c>LookupPanel</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presentation-agnostic: <see cref="LookupDialog"/> hosts it in a modal overlay, but it can be placed
    /// anywhere. It loads the rows when it receives a connector, and again on each search.
    /// </para>
    /// <para>
    /// A click selects a row; a double-click, or OK, commits it through <see cref="OnCommitted"/>. A load
    /// failure is shown on the panel, which stays usable, so the user can search again.
    /// </para>
    /// </remarks>
    public sealed partial class LookupPanel : ComponentBase
    {
        private LayoutGrid? _layout;
        private DataTable? _rows;
        private Guid? _selectedRowId;
        private string? _error;
        private IFormApiConnector? _loadedConnector;

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

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

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
            try
            {
                var response = await connector.GetLookupAsync(SearchText, cancellationToken: cancellationToken)
                    .ConfigureAwait(true);
                _rows = response.Table;
                if (_selectedRowId is { } id && FindRow(id) is null)
                    _selectedRowId = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // UI boundary: surface the failure on the panel instead of breaking the picker,
                // as the Avalonia panel does.
                _error = ex.Message;
            }
        }

        // A commit without a selection is a no-op.
        private async Task CommitAsync()
        {
            if (SelectedRow is { } row)
                await OnCommitted.InvokeAsync(row).ConfigureAwait(true);
        }

        private Task CancelAsync() => OnCancelled.InvokeAsync();

        private Task SearchAsync() => ReloadAsync();

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

        private void Select(Guid rowId) => _selectedRowId = rowId;

        private async Task ActivateAsync(Guid rowId)
        {
            _selectedRowId = rowId;
            await CommitAsync().ConfigureAwait(true);
        }

        private DataRow? FindRow(Guid rowId)
            => _rows?.Rows.Cast<DataRow>()
                .FirstOrDefault(r => FormDataGuard.TryGetRowId(r, out var id) && id == rowId);
    }
}
