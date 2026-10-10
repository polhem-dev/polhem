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
        /// Gets the API key the remote endpoint issued for this application; empty when <see cref="Mode"/> is
        /// <see cref="PolhemBlazorProviderMode.Local"/>.
        /// </summary>
        public string ApiKey { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets the deployment's default language, the last hop of the language fall-back chain for the
        /// components' own text and for the definitions a <see cref="Polhem.Web.Blazor.Server.Components.FormPage"/>
        /// localizes.
        /// </summary>
        /// <remarks>
        /// A setting of the deployment, the same for every circuit, which is why it lives here and not on a circuit's
        /// client. Blank drops the hop. A host whose in-process backend registers its language resources does not
        /// need it for the components' text, which then answers from those resources.
        /// </remarks>
        public string DefaultLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether a <see cref="Polhem.Web.Blazor.Server.Components.FormPage"/> that was not given a definition
        /// loader of its own assembles its definitions through
        /// a <see cref="Polhem.Api.Client.Definitions.FormDefinitionLoader"/> over the circuit's client.
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
        /// so that each circuit's <see cref="Polhem.Api.Client.PolhemApiClient"/> can dispatch its calls in process.
        /// </summary>
        /// <remarks>
        /// WARNING: for trusted users only. Every call becomes a trusted local call that skips the
        /// token and <c>LocalOnly</c> checks; see <see cref="PolhemBlazorProviderMode.Local"/>.
        /// </remarks>
        public PolhemBlazorOptions UseLocalProvider()
        {
            Mode = PolhemBlazorProviderMode.Local;
            Endpoint = string.Empty;
            ApiKey = string.Empty;
            return this;
        }

        /// <summary>
        /// Configures the HTTP (<see cref="PolhemBlazorProviderMode.Remote"/>)
        /// provider. Each circuit's client sends its calls to <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="endpoint">The remote API endpoint URL (must be non-empty).</param>
        /// <param name="apiKey">
        /// The key the server issued for this application, sent as the <c>X-Api-Key</c> header. The server's
        /// default <see cref="Polhem.Api.Core.Authorization.ApiAuthorizationValidator"/> refuses every method except
        /// <c>System.Ping</c> without an accepted key, so with an empty one the first call (usually the sign-in)
        /// fails with <c>401 Unauthorized</c>. The key identifies the calling application, not a circuit's user, so
        /// every circuit sends the same one.
        /// </param>
        public PolhemBlazorOptions UseRemoteProvider(string endpoint, string apiKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
            ArgumentNullException.ThrowIfNull(apiKey);
            Mode = PolhemBlazorProviderMode.Remote;
            Endpoint = endpoint;
            ApiKey = apiKey;
            return this;
        }
    }
}
