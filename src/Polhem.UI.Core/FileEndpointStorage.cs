using System.Reflection;
using Polhem.Core;

namespace Polhem.UI.Core
{
    /// <summary>
    /// File-backed <see cref="IEndpointStorage"/> and <see cref="IApiKeyStorage"/>. Persists the service endpoint
    /// and the API key as single-line UTF-8 text files in a per-application folder under the per-user local
    /// application data directory. It is the default for both <see cref="ClientInfo.EndpointStorage"/> and
    /// <see cref="ClientInfo.ApiKeyStorage"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The folder is <c>Environment.SpecialFolder.LocalApplicationData</c> combined with the application name.
    /// The runtime decides what that special folder is; on current .NET runtimes it resolves to:
    /// <list type="bullet">
    ///   <item>Windows: <c>%LOCALAPPDATA%\&lt;appName&gt;</c></item>
    ///   <item>macOS: <c>~/Library/Application Support/&lt;appName&gt;</c></item>
    ///   <item>Linux: <c>$XDG_DATA_HOME/&lt;appName&gt;</c>, or <c>~/.local/share/&lt;appName&gt;</c> when the
    ///   variable is not set</item>
    ///   <item>iOS: <c>Documents/&lt;appName&gt;</c> inside the app's sandbox container</item>
    ///   <item>Android: <c>files/.local/share/&lt;appName&gt;</c> inside the app's private data directory</item>
    /// </list>
    /// <see cref="FilePath"/> and <see cref="ApiKeyFilePath"/> report the paths actually in use. On iOS the
    /// Documents directory is included in device backups, and is visible in the Files app if the app opts in to
    /// file sharing.
    /// </para>
    /// <para>
    /// Browser WASM has no persistent file system: a write lands in an in-memory one and is gone after a reload.
    /// A browser host replaces both <see cref="ClientInfo.EndpointStorage"/> and
    /// <see cref="ClientInfo.ApiKeyStorage"/> with an implementation backed by browser storage.
    /// </para>
    /// <para>
    /// The endpoint is kept in <c>endpoint.txt</c> and the API key in <c>apikey.txt</c> beside it. Separate files
    /// rather than one keyed file: each value is written independently, and a single-line file needs no format to
    /// go wrong.
    /// </para>
    /// <para>
    /// <see cref="SetEndpoint"/> and <see cref="SetApiKey"/> change an in-memory cache only; the files are written
    /// solely by <see cref="SaveEndpoint"/> and <see cref="SaveApiKey"/>, so a bound input does not cause disk
    /// traffic on every keystroke.
    /// </para>
    /// </remarks>
    public sealed class FileEndpointStorage : IEndpointStorage, IApiKeyStorage
    {
        private readonly string _filePath;
        private readonly string _apiKeyFilePath;
        private string? _cachedEndpoint;
        private string? _cachedApiKey;

        /// <summary>
        /// Initializes a new instance of <see cref="FileEndpointStorage"/>.
        /// </summary>
        /// <param name="appName">
        /// Application folder name appended to the per-user local application data directory.
        /// Must be a single path segment (no separators); the constructor does not validate
        /// this beyond rejecting null / whitespace.
        /// </param>
        public FileEndpointStorage(string appName)
            : this(appName, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FileEndpointStorage"/> under an explicit root directory,
        /// so tests can keep the files out of the real user profile.
        /// </summary>
        /// <param name="appName">Application folder name appended to <paramref name="rootDirectory"/>.</param>
        /// <param name="rootDirectory">The directory that takes the place of the local application data directory.</param>
        internal FileEndpointStorage(string appName, string rootDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appName);

            _filePath = Path.Combine(rootDirectory, appName, "endpoint.txt");
            _apiKeyFilePath = Path.Combine(rootDirectory, appName, "apikey.txt");
        }

        /// <summary>
        /// Gets the folder name of the instance <see cref="ClientInfo"/> creates as its default storage: the entry
        /// assembly's name, or <c>Polhem</c> when the runtime reports no entry assembly.
        /// </summary>
        internal static string DefaultAppName => Assembly.GetEntryAssembly()?.GetName().Name ?? "Polhem";

        /// <summary>
        /// Gets the absolute path of the file backing the endpoint.
        /// </summary>
        public string FilePath => _filePath;

        /// <summary>
        /// Gets the absolute path of the file backing the API key.
        /// </summary>
        public string ApiKeyFilePath => _apiKeyFilePath;

        /// <inheritdoc/>
        public string LoadEndpoint()
        {
            _cachedEndpoint ??= File.Exists(_filePath)
                ? File.ReadAllText(_filePath).Trim()
                : string.Empty;
            return _cachedEndpoint;
        }

        /// <inheritdoc/>
        public void SetEndpoint(string endpoint)
        {
            _cachedEndpoint = endpoint;
        }

        /// <inheritdoc/>
        public void SaveEndpoint(string endpoint)
        {
            _cachedEndpoint = endpoint;
            EnsureDirectory(_filePath);
            File.WriteAllText(_filePath, endpoint);
        }

        /// <inheritdoc/>
        public string LoadApiKey()
        {
            _cachedApiKey ??= File.Exists(_apiKeyFilePath)
                ? File.ReadAllText(_apiKeyFilePath).Trim()
                : string.Empty;
            return _cachedApiKey;
        }

        /// <inheritdoc/>
        public void SetApiKey(string apiKey)
        {
            _cachedApiKey = apiKey;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The key is a long-lived application credential, so unlike the endpoint it is written
        /// owner-only (<see cref="FileUtilities.FileWriteOwnerOnlyText"/>): a default-permission file
        /// would be readable by every account on a shared Unix host.
        /// </remarks>
        public void SaveApiKey(string apiKey)
        {
            _cachedApiKey = apiKey;
            EnsureDirectory(_apiKeyFilePath);
            FileUtilities.FileWriteOwnerOnlyText(_apiKeyFilePath, apiKey, overwrite: true);
        }

        private static void EnsureDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
