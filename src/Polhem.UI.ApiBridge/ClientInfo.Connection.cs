using System.Diagnostics.CodeAnalysis;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Core;
using System.Net.Sockets;

namespace Polhem.UI.ApiBridge
{
    /// <summary>
    /// Connection members of <see cref="ClientInfo"/>: the endpoint, the API key, and initializing the connection from
    /// the stored settings and the command line. Split out for file size only; behaviour is unchanged.
    /// </summary>
    public static partial class ClientInfo
    {
        private static void SetConnectType(ConnectType connectType, string endpoint)
        {
            // NOTE: a new client means a new connection, which always needs a fresh sign-in: its session starts
            // anonymous, with no time zone, so no zone outlives the session it belonged to (ADR-032 D13). The
            // identity-scoped caches go in the same step, so no reader pairs the new client with the previous
            // identity's definitions or capabilities.
            var client = connectType == ConnectType.Local
                ? CreateLocalClient()
                : PolhemApiClient.CreateRemote(endpoint, s_apiKey);
            lock (s_stateGate)
            {
                s_client = client;
                DiscardIdentityState();
            }
        }

        /// <summary>
        /// Sets the service endpoint and persists it, awaiting the validation and connector
        /// initialization instead of blocking on them.
        /// </summary>
        /// <param name="endpoint">URL for remote connections; local file path for local connections.</param>
        /// <param name="cancellationToken">A token that cancels the validation and the connector initialization.</param>
        /// <remarks>
        /// Validates the endpoint and initializes the connector without blocking, so it is safe on
        /// single-threaded runtimes (browser WASM), where blocking on async work throws
        /// "Cannot wait on monitors on this runtime".
        /// </remarks>
        public static async Task SetEndpointAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var connectType = await ApiConnectValidator.ValidateAsync(endpoint, SupportedConnectTypes, AllowGenerateSettings, cancellationToken)
                .ConfigureAwait(false);
            SetConnectType(connectType, endpoint);
            await SystemApiConnector.InitializeAsync(cancellationToken).ConfigureAwait(false);
            EndpointStorage.SaveEndpoint(endpoint);
        }

        /// <summary>
        /// Returns the currently configured service endpoint.
        /// </summary>
        public static string GetEndpoint()
        {
            return EndpointStorage.LoadEndpoint();
        }

        /// <summary>
        /// Returns the currently configured API key.
        /// </summary>
        public static string GetApiKey()
        {
            return ApiKeyStorage.LoadApiKey();
        }

        /// <summary>
        /// Persists the API key and applies it to subsequent API calls.
        /// </summary>
        /// <param name="apiKey">The API key issued for this application.</param>
        /// <remarks>
        /// Synchronous, unlike <see cref="SetEndpointAsync"/>: changing the endpoint has to
        /// revalidate the connection and rebuild the connector, whereas the key is simply a header
        /// value the next call carries.
        /// </remarks>
        public static void SetApiKey(string apiKey)
        {
            ApiKeyStorage.SaveApiKey(apiKey);
            UseApiKey(apiKey);
        }

        /// <summary>
        /// Applies the stored API key to <see cref="ApiClient"/>, falling back to
        /// <paramref name="defaultApiKey"/> — and persisting it — the first time an application runs
        /// with nothing stored.
        /// </summary>
        /// <param name="defaultApiKey">
        /// The key the application ships with, used only to seed empty storage. Pass an empty string
        /// for deployments that expect the key to be configured out of band.
        /// </param>
        /// <remarks>
        /// This is what lets an application drop its hard-coded key without losing out-of-the-box
        /// behaviour: the shipped value becomes a first-run seed, and from then on the stored value
        /// wins and can be changed without recompiling.
        /// </remarks>
        public static void ApplyApiKey(string defaultApiKey = "")
        {
            string stored = ApiKeyStorage.LoadApiKey();
            if (StringUtilities.IsEmpty(stored) && StringUtilities.IsNotEmpty(defaultApiKey))
            {
                ApiKeyStorage.SaveApiKey(defaultApiKey);
                stored = defaultApiKey;
            }
            UseApiKey(stored);
        }

        // The key outlives the client: an endpoint change creates a new client, which starts with this key.
        private static void UseApiKey(string apiKey)
        {
            lock (s_stateGate)
            {
                s_apiKey = apiKey;
                s_client?.ApiKey = apiKey;
            }
        }

        private static async Task<bool> InitializeConnectAsync(SupportedConnectTypes supportedConnectTypes,
            CancellationToken cancellationToken)
        {
            SupportedConnectTypes = supportedConnectTypes;
            try
            {
                string endpoint = GetEndpoint();
                var connectType = await ApiConnectValidator.ValidateAsync(endpoint, SupportedConnectTypes, AllowGenerateSettings, cancellationToken)
                    .ConfigureAwait(false);
                SetConnectType(connectType, endpoint);
                await SystemApiConnector.InitializeAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            // A cancellation the caller asked for is not an unreachable endpoint: it propagates rather
            // than sending the host to its connection setup.
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException
                or IOException or SocketException or UriFormatException
                || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // Returning false sends the host to its connection setup, so only "the
                // endpoint is missing, malformed or unreachable" belongs here. The first two entries
                // are the vocabulary this path actually raises — `ApiConnectValidator` reports an
                // unreachable endpoint as `InvalidOperationException` and an empty one as
                // `ArgumentException`; the rest cover the transport failures underneath it.
                return false;
            }
        }

        /// <summary>
        /// Initializes from the stored settings and the command line.
        /// </summary>
        /// <param name="connectTypes">Connection types supported by the application.</param>
        /// <param name="cancellationToken">
        /// A token that cancels the connection attempt. A cancelled call throws
        /// <see cref="OperationCanceledException"/> rather than returning <c>false</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> when the stored endpoint was reached; <c>false</c> when it is missing, malformed or
        /// unreachable, in which case the host shows its own connection setup and then calls
        /// <see cref="InitializeAsync(string, CancellationToken)"/> with the endpoint the user entered.
        /// </returns>
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple overloads with optional parameters",
            Justification = "The only optional parameter is the trailing cancellation token, and the two overloads differ in their first parameter (the supported connection types or an endpoint string), so no call can rebind from one to the other, which is the hazard RS0026 guards against.")]
        public static async Task<bool> InitializeAsync(SupportedConnectTypes connectTypes,
            CancellationToken cancellationToken = default)
        {
            Arguments = ParseCommandLineArgs();
            if (Arguments.TryGetValue("Endpoint", out string? endpointArg))
            {
                EndpointStorage.SetEndpoint(endpointArg);
            }
            // NOTE: in-memory only, like the endpoint argument — a command line switch overrides
            // this run without rewriting stored settings. Suitable because the key identifies an
            // application rather than authenticating a user; a real credential does not belong in
            // an argument list, which is readable from the process table.
            if (Arguments.TryGetValue("ApiKey", out string? apiKeyArg))
            {
                ApiKeyStorage.SetApiKey(apiKeyArg);
            }
            return await InitializeConnectAsync(connectTypes, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Initializes with an explicit endpoint, awaiting the validation and connector
        /// initialization without blocking.
        /// </summary>
        /// <param name="endpoint">URL for remote connections; local file path for local connections.</param>
        /// <param name="cancellationToken">A token that cancels the validation and the connector initialization.</param>
        /// <remarks>
        /// Safe on single-threaded runtimes (browser WASM), where blocking on async work throws
        /// "Cannot wait on monitors".
        /// </remarks>
        [SuppressMessage("ApiDesign", "RS0026:Do not add multiple overloads with optional parameters",
            Justification = "The only optional parameter is the trailing cancellation token, and the two overloads differ in their first parameter (the supported connection types or an endpoint string), so no call can rebind from one to the other, which is the hazard RS0026 guards against.")]
        public static Task InitializeAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            return SetEndpointAsync(endpoint, cancellationToken);
        }

        private static Dictionary<string, string> ParseCommandLineArgs()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                int sep = args[i].IndexOf('=');
                if (sep > 0)
                {
                    string key = args[i].Substring(0, sep);
                    string value = args[i].Substring(sep + 1);
                    result[key] = value;
                }
            }
            return result;
        }
    }
}
