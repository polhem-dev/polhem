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
        /// <remarks>
        /// WARNING: every call made in this mode is a trusted local call, whichever browser user caused
        /// it. The backend skips the access token check and the <c>LocalOnly</c> restriction for it, and
        /// the checks that business objects key on the local-call flag let it through as well (for
        /// example saving definitions, minting sessions without credentials, managing API keys). Use it
        /// only when every user of the site is trusted with the whole backend; otherwise use
        /// <see cref="Remote"/>, where calls pass the same checks as any other API client.
        /// </remarks>
        Local = 0,

        /// <summary>
        /// Over HTTP: connectors use <see cref="Polhem.Api.Client.Providers.RemoteApiProvider"/> against
        /// <see cref="PolhemBlazorOptions.Endpoint"/>.
        /// </summary>
        Remote = 1,
    }
}
