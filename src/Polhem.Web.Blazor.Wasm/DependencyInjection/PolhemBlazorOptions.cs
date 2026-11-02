using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Wasm.DependencyInjection
{
    /// <summary>
    /// The settings of <see cref="PolhemBlazorServiceCollectionExtensions.AddPolhemBlazor"/>: which Polhem API server
    /// the page calls, and how components assemble definitions.
    /// </summary>
    /// <remarks>
    /// There is no in-process mode: the backend cannot run in the browser, so every call is checked by the server like
    /// any other API client's.
    /// </remarks>
    public sealed class PolhemBlazorOptions : IBlazorDefinitionOptions
    {
        /// <summary>
        /// Gets or sets the absolute address of the API endpoint. A server on another origin must allow this page's
        /// origin through CORS.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the key the server issued for this application, sent as the <c>X-Api-Key</c> header.
        /// </summary>
        /// <remarks>
        /// WARNING: everything in a WebAssembly bundle is public, so this key is too. It identifies the application to
        /// the server and does not protect anything. Do not reuse a key that a trusted client relies on.
        /// </remarks>
        public string ApiKey { get; set; } = string.Empty;

        /// <inheritdoc/>
        public string DefaultLanguage { get; set; } = string.Empty;

        /// <inheritdoc/>
        public bool UseDefinitionLoader { get; set; } = true;
    }
}
