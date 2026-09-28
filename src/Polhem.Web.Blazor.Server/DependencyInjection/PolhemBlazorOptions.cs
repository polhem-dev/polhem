namespace Polhem.Web.Blazor.Server.DependencyInjection
{
    /// <summary>
    /// Fluent options used by <see cref="PolhemBlazorServiceCollectionExtensions.AddPolhemBlazor"/>
    /// to decide whether Blazor Server components talk to the backend in-process
    /// (<see cref="PolhemBlazorProviderMode.Local"/>) or over HTTP
    /// (<see cref="PolhemBlazorProviderMode.Remote"/>).
    /// </summary>
    /// <remarks>
    /// WARNING: <see cref="PolhemBlazorProviderMode.Local"/>, the default, treats every browser user's
    /// call as a trusted in-process call: the backend skips the access token check and the
    /// <c>LocalOnly</c> restriction, and the business-object checks keyed on the local-call flag pass
    /// too. That is only appropriate when every user of the site is trusted with the whole backend,
    /// such as an internal administration tool. A site that serves users who must be held to their
    /// own permissions should call <see cref="UseRemoteProvider"/> and reach the backend over HTTP,
    /// where each call is checked like any other API client's.
    /// </remarks>
    public sealed class PolhemBlazorOptions
    {
        /// <summary>
        /// Gets the resolved provider mode (<c>Local</c> by default for Blazor Server).
        /// </summary>
        public PolhemBlazorProviderMode Mode { get; private set; } = PolhemBlazorProviderMode.Local;

        /// <summary>
        /// Gets the remote endpoint URL; empty when <see cref="Mode"/> is
        /// <see cref="PolhemBlazorProviderMode.Local"/>.
        /// </summary>
        public string Endpoint { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether a <see cref="Polhem.Web.Blazor.Server.Components.FormPage"/> that was not given a definition
        /// loader of its own assembles its definitions through
        /// <see cref="PolhemApiConnectorFactory.CreateDefinitionLoader"/>.
        /// </summary>
        /// <remarks>
        /// The counterpart of the desktop client's <c>ClientInfo.UseDefinitionLoader</c>, and on by
        /// default for the same reason: pages show captions in the circuit's UI culture, the tenant's
        /// customized layouts and the framework's number formats. <c>false</c> renders definitions
        /// exactly as stored, which saves the loader's extra round trips for both language layers and
        /// both layout layers.
        /// </remarks>
        public bool UseDefinitionLoader { get; set; } = true;

        /// <summary>
        /// Configures the in-process (<see cref="PolhemBlazorProviderMode.Local"/>)
        /// provider. The host must also call <c>AddPolhemFramework</c> on the same service collection,
        /// so that <see cref="PolhemApiConnectorFactory"/> can dispatch connector calls in process.
        /// </summary>
        /// <remarks>
        /// WARNING: for trusted users only. Every call becomes a trusted local call that skips the
        /// token and <c>LocalOnly</c> checks; see <see cref="PolhemBlazorProviderMode.Local"/>.
        /// </remarks>
        public PolhemBlazorOptions UseLocalProvider()
        {
            Mode = PolhemBlazorProviderMode.Local;
            Endpoint = string.Empty;
            return this;
        }

        /// <summary>
        /// Configures the HTTP (<see cref="PolhemBlazorProviderMode.Remote"/>)
        /// provider. Connector calls are dispatched to <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="endpoint">The remote API endpoint URL (must be non-empty).</param>
        public PolhemBlazorOptions UseRemoteProvider(string endpoint)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
            Mode = PolhemBlazorProviderMode.Remote;
            Endpoint = endpoint;
            return this;
        }
    }
}
