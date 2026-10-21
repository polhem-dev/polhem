using System.Runtime.Versioning;
using Polhem.UI.ApiBridge;
using Polhem.UI.Avalonia.Shell.Preferences;

namespace Polhem.UI.Avalonia.Shell.Browser
{
    /// <summary>
    /// <see cref="IEndpointStorage"/>, <see cref="IApiKeyStorage"/> and <see cref="IShellPreferenceStorage"/> backed by
    /// the browser's <c>window.localStorage</c>, for a browser (WebAssembly) head.
    /// </summary>
    /// <remarks>
    /// The browser runtime has no persistent file system, so the default file-backed storage loses its writes on
    /// reload. A browser head assigns one instance to <see cref="ClientInfo.EndpointStorage"/>,
    /// <see cref="ClientInfo.ApiKeyStorage"/> and <see cref="ShellOptions.PreferenceStorage"/> before any code calls
    /// <see cref="ClientInfo.InitializeAsync(string, System.Threading.CancellationToken)"/>.
    /// <para>
    /// Mirroring <see cref="FileEndpointStorage"/>, <see cref="SetEndpoint"/> and <see cref="SetApiKey"/> change an
    /// in-memory cache only; the localStorage entry is written by <see cref="SaveEndpoint"/> and
    /// <see cref="SaveApiKey"/>, so a bound input does not cross into JavaScript on every keystroke.
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("browser")]
    public sealed class BrowserLocalStorage : IEndpointStorage, IApiKeyStorage, IShellPreferenceStorage
    {
        private const string EndpointPrefix = "Polhem.Endpoint.";
        private const string ApiKeyPrefix = "Polhem.ApiKey.";
        private const string PreferencePrefix = "Polhem.Preference.";
        private readonly string _endpointKey;
        private readonly string _apiKeyKey;
        private readonly string _preferencePrefix;
        private string? _cachedEndpoint;
        private string? _cachedApiKey;

        /// <summary>
        /// Initializes a new instance of <see cref="BrowserLocalStorage"/>.
        /// </summary>
        /// <param name="appName">
        /// The application name that namespaces the localStorage keys, so applications served from one origin do not
        /// share values.
        /// </param>
        public BrowserLocalStorage(string appName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appName);
            _endpointKey = EndpointPrefix + appName;
            _apiKeyKey = ApiKeyPrefix + appName;
            _preferencePrefix = PreferencePrefix + appName + ".";
        }

        /// <inheritdoc/>
        public string LoadEndpoint()
        {
            _cachedEndpoint ??= LocalStorageInterop.GetItem(_endpointKey) ?? string.Empty;
            return _cachedEndpoint;
        }

        /// <inheritdoc/>
        public void SetEndpoint(string endpoint) => _cachedEndpoint = endpoint;

        /// <inheritdoc/>
        public void SaveEndpoint(string endpoint)
        {
            _cachedEndpoint = endpoint;
            LocalStorageInterop.SetItem(_endpointKey, endpoint);
        }

        /// <inheritdoc/>
        public string LoadApiKey()
        {
            _cachedApiKey ??= LocalStorageInterop.GetItem(_apiKeyKey) ?? string.Empty;
            return _cachedApiKey;
        }

        /// <inheritdoc/>
        public void SetApiKey(string apiKey) => _cachedApiKey = apiKey;

        /// <inheritdoc/>
        public void SaveApiKey(string apiKey)
        {
            _cachedApiKey = apiKey;
            LocalStorageInterop.SetItem(_apiKeyKey, apiKey);
        }

        /// <inheritdoc/>
        public string Load(string key) => LocalStorageInterop.GetItem(_preferencePrefix + key) ?? string.Empty;

        /// <inheritdoc/>
        public void Save(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                LocalStorageInterop.RemoveItem(_preferencePrefix + key);
            else
                LocalStorageInterop.SetItem(_preferencePrefix + key, value);
        }
    }
}
