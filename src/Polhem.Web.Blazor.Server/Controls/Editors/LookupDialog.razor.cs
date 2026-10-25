using System.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.Api.Client.Definitions;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.UI.Core.Definitions;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Modal presentation of <see cref="LookupPanel"/>: shows the lookup picker for a target program over
    /// the page and reports the committed row through <see cref="OnCommitted"/>, or a cancellation through
    /// <see cref="OnCancelled"/>. The counterpart of the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.LookupDialog</c>; the host
    /// renders it while the lookup is open and removes it when either callback runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The schema and the connector are resolved in this order, the order of the Avalonia dialog: the
    /// <see cref="Schema"/> and <see cref="Connector"/> parameters; then an
    /// <see cref="IFormDefinitionSource"/> and an <see cref="IFormApiConnectorFactory"/> registered in the
    /// container, which is how a host without a Polhem backend plugs in its own; then the circuit's
    /// <see cref="PolhemApiClient"/>, loading the schema the way <see cref="FormPage"/> loads its own, so the
    /// dialog's title and columns read in the same language as the form. Register those services per
    /// circuit (scoped): this head serves several users from one process.
    /// </para>
    /// <para>
    /// A failure to load the schema or create the connector is shown in the dialog and reported through
    /// <see cref="OnFailed"/>.
    /// </para>
    /// </remarks>
    public sealed partial class LookupDialog : ComponentBase
    {
        private FormSchema? _schema;
        private IFormApiConnector? _connector;
        private string? _error;
        // The parameters the schema and the connector were last resolved from.
        private (string ProgId, FormSchema? Schema, IFormApiConnector? Connector)? _resolvedFrom;

        /// <summary>
        /// Gets or sets the target program identifier (the lookup source form).
        /// </summary>
        [Parameter, EditorRequired]
        public string ProgId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target form's schema; <c>null</c> loads it through the circuit's client.
        /// </summary>
        [Parameter]
        public FormSchema? Schema { get; set; }

        /// <summary>
        /// Gets or sets the connector for the target form; <c>null</c> uses the circuit's client.
        /// </summary>
        [Parameter]
        public IFormApiConnector? Connector { get; set; }

        /// <summary>
        /// Gets or sets the loader the schema is assembled with, for this dialog only; <c>null</c> uses the
        /// same default as <see cref="FormPage"/>.
        /// </summary>
        [Parameter]
        public FormDefinitionLoader? DefinitionLoader { get; set; }

        /// <summary>
        /// Invoked with the committed row.
        /// </summary>
        [Parameter]
        public EventCallback<DataRow> OnCommitted { get; set; }

        /// <summary>
        /// Invoked when the user cancels (the Cancel button or a click outside the card).
        /// </summary>
        [Parameter]
        public EventCallback OnCancelled { get; set; }

        /// <summary>
        /// Invoked when the schema or the connector cannot be obtained.
        /// </summary>
        [Parameter]
        public EventCallback<Exception> OnFailed { get; set; }

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string Title => _schema?.DisplayName ?? ProgId;

        private string CancelText => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), PolhemUIText.Cancel);

        private string LoadingText => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), PolhemUIText.Loading);

        /// <inheritdoc />
        protected override async Task OnParametersSetAsync()
        {
            var parameters = (ProgId, Schema, Connector);
            if (_resolvedFrom is { } previous
                && string.Equals(previous.ProgId, parameters.ProgId, StringComparison.Ordinal)
                && ReferenceEquals(previous.Schema, parameters.Schema)
                && ReferenceEquals(previous.Connector, parameters.Connector))
            {
                return;
            }
            _resolvedFrom = parameters;
            _error = null;
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(ProgId);
                _schema = Schema ?? await LoadSchemaAsync().ConfigureAwait(true);
                _connector = Connector
                    ?? Services?.GetService<IFormApiConnectorFactory>()?.Create(ProgId)
                    ?? RequireClient().Form(ProgId);
            }
            catch (Exception ex)
            {
                // UI boundary: the dialog shows the failure instead of breaking the page, and the host hears
                // about it through OnFailed.
                _schema = null;
                _connector = null;
                _error = ex.Message;
                await OnFailed.InvokeAsync(ex).ConfigureAwait(true);
            }
        }

        private async Task<FormSchema> LoadSchemaAsync()
        {
            if (Services?.GetService<IFormDefinitionSource>() is { } source)
                return await source.GetFormSchemaAsync(ProgId).ConfigureAwait(true);
            var client = RequireClient();
            var options = Services?.GetService<PolhemBlazorOptions>() ?? new PolhemBlazorOptions();
            var loader = CircuitDefinitions.CreateLoader(client, options, DefinitionLoader);
            return await CircuitDefinitions.LoadSchemaAsync(client, loader, ProgId).ConfigureAwait(true)
                ?? throw new InvalidOperationException($"FormSchema '{ProgId}' was not found for lookup.");
        }

        private PolhemApiClient RequireClient()
            => Services?.GetService<PolhemApiClient>()
                ?? throw new InvalidOperationException(
                    $"No source for lookup '{ProgId}': pass a Schema and a Connector, register an "
                    + $"{nameof(IFormDefinitionSource)} and an {nameof(IFormApiConnectorFactory)}, or register the "
                    + $"circuit's {nameof(PolhemApiClient)} with AddPolhemBlazor.");

        private Task CommitAsync(DataRow row) => OnCommitted.InvokeAsync(row);

        private Task CancelAsync() => OnCancelled.InvokeAsync();
    }
}
