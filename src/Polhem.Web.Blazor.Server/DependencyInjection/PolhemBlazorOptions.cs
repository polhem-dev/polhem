namespace Polhem.Web.Blazor.Server.DependencyInjection
{
    /// <summary>
    /// Fluent options used by <see cref="PolhemBlazorServiceCollectionExtensions.AddPolhemBlazor"/>
    /// to decide whether Blazor Server components talk to the backend in-process
    /// (<see cref="PolhemBlazorProviderMode.Local"/>) or over HTTP
    /// (<see cref="PolhemBlazorProviderMode.Remote"/>).
    /// </summary>
    /// <remarks>
    /// Blazor Server hosts typically share a process with the backend
    /// (<see cref="UseLocalProvider"/>); Server deployments that talk to a
    /// separate API host can still opt into <see cref="UseRemoteProvider"/>.
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
        /// Configures the in-process (<see cref="PolhemBlazorProviderMode.Local"/>)
        /// provider. The host must also call <c>AddPolhemFramework</c> and assign
        /// <see cref="Polhem.Api.Client.ApiClientInfo.LocalServiceProvider"/> so connector calls can be
        /// dispatched in process.
        /// </summary>
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
