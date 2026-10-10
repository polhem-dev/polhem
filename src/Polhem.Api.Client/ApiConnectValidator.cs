using Polhem.Definition.Settings;
using Polhem.Core;
using Polhem.Core.Serialization;

namespace Polhem.Api.Client
{
    /// <summary>
    /// Validator for API service connection settings.
    /// </summary>
    public static class ApiConnectValidator
    {
        /// <summary>
        /// Validates the input service endpoint and returns the corresponding connection type.
        /// </summary>
        /// <param name="endpoint">The endpoint to validate: a URL for remote connections or a local path for local connections.</param>
        /// <param name="supportedConnectTypes">The connection types the application allows; any other is refused.</param>
        /// <param name="allowGenerateSettings">Whether to auto-generate missing settings files (SystemSettings.xml and DatabaseSettings.xml) for local connections.</param>
        /// <param name="cancellationToken">A token that cancels remote validation.</param>
        /// <remarks>
        /// Remote validation awaits the ping instead of blocking on it,
        /// so it is safe on single-threaded runtimes (browser WASM) where blocking would throw
        /// "Cannot wait on monitors".
        /// </remarks>
        public static async Task<ConnectType> ValidateAsync(string endpoint, SupportedConnectTypes supportedConnectTypes,
            bool allowGenerateSettings = false, CancellationToken cancellationToken = default)
        {
            if (StringUtilities.IsEmpty(endpoint))
                throw new ArgumentException("Input cannot be null or empty.", nameof(endpoint));

            if (FileUtilities.IsLocalPath(endpoint))
            {
                // Local validation is pure file-system I/O with no async work to await.
                ValidateLocal(endpoint, supportedConnectTypes, allowGenerateSettings);
                return ConnectType.Local;
            }
            else if (HttpUtilities.IsUrl(endpoint))
            {
                await ValidateRemoteAsync(endpoint, supportedConnectTypes, cancellationToken).ConfigureAwait(false);
                return ConnectType.Remote;
            }
            else
            {
                throw new InvalidOperationException("Unrecognized connection type. Please enter a valid service endpoint or local path.");
            }
        }

        /// <summary>
        /// Validates the local connection settings.
        /// </summary>
        /// <param name="definePath">The definition path.</param>
        /// <param name="supportedConnectTypes">The connection types the application allows.</param>
        /// <param name="allowGenerateSettings">Whether to auto-generate missing settings files for local connections.</param>
        private static void ValidateLocal(string definePath, SupportedConnectTypes supportedConnectTypes, bool allowGenerateSettings)
        {
            // Verify the application supports local connections
            if (!supportedConnectTypes.HasFlag(SupportedConnectTypes.Local))
                throw new InvalidOperationException("Local connections are not supported.");
            if (StringUtilities.IsEmpty(definePath))
                throw new ArgumentException("Definition path must be specified.", nameof(definePath));

            if (allowGenerateSettings) // Auto-generate missing settings files (used by tool applications)
            {
                // Verify SystemSettings.xml exists; create it if missing
                ValidateSystemSettings(definePath);
                // Verify DatabaseSettings.xml exists; create it if missing
                ValidateDatabaseSettings(definePath);
            }
            else // Settings files must already exist (used by regular applications)
            {
                if (!Directory.Exists(definePath))
                    throw new ArgumentException("Definition path does not exist.", nameof(definePath));
                // Verify that SystemSettings.xml exists in the specified path
                string filePath = Path.Combine(definePath, "SystemSettings.xml");
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("SystemSettings.xml file not found in the definition path.", filePath);
            }
        }

        /// <summary>
        /// Verifies that SystemSettings.xml exists in the definition path, creating it if missing.
        /// </summary>
        /// <param name="definePath">The definition path.</param>
        private static void ValidateSystemSettings(string definePath)
        {
            // Check for SystemSettings.xml; create it if not found
            string filePath = Path.Combine(definePath, "SystemSettings.xml");
            if (!File.Exists(filePath))
            {
                var settings = new SystemSettings();
                settings.SetObjectFilePath(filePath);
                settings.Save();
            }
        }

        /// <summary>
        /// Verifies that DatabaseSettings.xml exists in the definition path, creating it if missing.
        /// </summary>
        /// <param name="definePath">The definition path.</param>
        private static void ValidateDatabaseSettings(string definePath)
        {
            // Check for DatabaseSettings.xml; create it if not found
            string filePath = Path.Combine(definePath, "DatabaseSettings.xml");
            if (!File.Exists(filePath))
            {
                var settings = new DatabaseSettings();
                var item = new DatabaseItem()
                {
                    Id = "default",
                    DisplayName = "Default Database"
                };
                settings.Items!.Add(item);
                settings.SetObjectFilePath(filePath);
                settings.Save();
            }
        }

        /// <summary>
        /// Validates the remote connection settings by pinging the endpoint.
        /// </summary>
        /// <remarks>
        /// The ping is the reachability check: a failure to reach the host is reported as an unreachable endpoint,
        /// while an HTTP or JSON-RPC error from a host that answered propagates as it is. A separate HTTP <c>HEAD</c>
        /// probe used to run first; the endpoint accepts only POST, so it was answered with 405 on every connect.
        /// </remarks>
        /// <param name="endpoint">The service endpoint.</param>
        /// <param name="supportedConnectTypes">The connection types the application allows.</param>
        /// <param name="cancellationToken">A token that cancels the ping.</param>
        private static async Task ValidateRemoteAsync(string endpoint, SupportedConnectTypes supportedConnectTypes,
            CancellationToken cancellationToken)
        {
            // Verify the application supports remote connections
            if (!supportedConnectTypes.HasFlag(SupportedConnectTypes.Remote))
                throw new InvalidOperationException("Remote connections are not supported.");
            if (StringUtilities.IsEmpty(endpoint))
                throw new ArgumentException("The endpoint must be specified.", nameof(endpoint));
            // The server answers `System.Ping` without an API key, so the probe needs none.
            var connector = PolhemApiClient.CreateRemote(endpoint, string.Empty).System;
            try
            {
                await connector.PingAsync(cancellationToken).ConfigureAwait(false);
            }
            // `PingAsync` wraps every failure except the caller's cancellation, so the cause is the inner exception.
            catch (InvalidOperationException ex) when (IsUnreachable(ex.InnerException))
            {
                throw new InvalidOperationException($"Endpoint not reachable: {endpoint}", ex);
            }
        }

        /// <summary>
        /// Says whether a ping failed before any HTTP response arrived.
        /// </summary>
        /// <param name="exception">The cause of the failure.</param>
        /// <returns>
        /// <see langword="true"/> for a request that got no response (DNS failure, a refused connection) or that
        /// timed out; <see langword="false"/> for an HTTP or JSON-RPC error from a host that answered.
        /// </returns>
        private static bool IsUnreachable(Exception? exception)
            => exception is HttpRequestException { StatusCode: null } or TaskCanceledException;
    }
}
