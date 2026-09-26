namespace Polhem.Web.Blazor.Server.DependencyInjection
{
    /// <summary>
    /// Provider mode selected via <see cref="PolhemBlazorOptions"/>.
    /// </summary>
    public enum PolhemBlazorProviderMode
    {
        /// <summary>
        /// In-process: the host is also the API backend; connectors use
        /// <see cref="Polhem.Api.Client.Providers.LocalApiProvider"/>.
        /// </summary>
        Local = 0,

        /// <summary>
        /// Over HTTP: connectors use <see cref="Polhem.Api.Client.Providers.RemoteApiProvider"/> against
        /// <see cref="PolhemBlazorOptions.Endpoint"/>.
        /// </summary>
        Remote = 1,
    }
}
