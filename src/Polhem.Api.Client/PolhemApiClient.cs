using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.Transformers;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client
{
    /// <summary>
    /// The entry point of the API client: one connection to a backend, the identity signed in over
    /// it, and the connectors that call it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client decides once whether calls run in process (<see cref="CreateLocal"/>) or over HTTP
    /// (<see cref="CreateRemote"/>), and every connector it hands out follows that choice. Signing in
    /// through <see cref="System"/> stores the access token, the transmission key and the user's time
    /// zone in <see cref="Session"/>, and every connector of the client calls with them from then on.
    /// </para>
    /// <code>
    /// var client = PolhemApiClient.CreateRemote("https://host/api", apiKey);
    /// await client.System.LoginAsync(userId, password);
    /// await client.System.EnterCompanyAsync(companyId);
    /// var data = await client.Form("Employee").GetDataAsync(rowId);
    /// </code>
    /// <para>
    /// One client holds one identity. A host that serves several users from one process, such as
    /// Blazor Server, creates a client per user.
    /// </para>
    /// </remarks>
    public sealed class PolhemApiClient
    {
        private readonly Func<Guid, IJsonRpcTransport> _transportFactory;
        private string _apiKey;

        private PolhemApiClient(Func<Guid, IJsonRpcTransport> transportFactory, bool isLocal, string endpoint,
            string apiKey, PayloadOptions? payloadOptions)
        {
            IsLocal = isLocal;
            Endpoint = endpoint;
            _apiKey = apiKey;
            PayloadOptions = payloadOptions ?? PolhemPayload.CreateOptions();
            _transportFactory = transportFactory;
            System = new SystemApiConnector(this);
            AuditLog = new AuditLogApiConnector(this);
        }

        /// <summary>
        /// Creates a client that dispatches every call to a backend running in this process.
        /// </summary>
        /// <param name="services">The in-process backend's service provider, built by <c>services.AddPolhemFramework(...)</c>.</param>
        /// <param name="payloadOptions">
        /// The payload options to read and write the envelope with; <c>null</c> creates the framework's defaults.
        /// </param>
        /// <returns>The client.</returns>
        public static PolhemApiClient CreateLocal(IServiceProvider services, PayloadOptions? payloadOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            return new PolhemApiClient(token => new LocalApiProvider(services, token), isLocal: true,
                string.Empty, string.Empty, payloadOptions);
        }

        /// <summary>
        /// Creates a client that sends every call over HTTP to <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="apiKey">
        /// The key the server issued for this application, sent as the <c>X-Api-Key</c> header. The server's default
        /// authorization refuses every method except <c>System.Ping</c> without an accepted key.
        /// </param>
        /// <param name="payloadOptions">
        /// The payload options to read and write the envelope with; <c>null</c> creates the framework's defaults.
        /// </param>
        /// <returns>The client.</returns>
        public static PolhemApiClient CreateRemote(string endpoint, string apiKey, PayloadOptions? payloadOptions = null)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty.", nameof(endpoint));
            ArgumentNullException.ThrowIfNull(apiKey);

            PolhemApiClient? client = null;
            client = new PolhemApiClient(token => new RemoteApiProvider(endpoint, token, () => client!.ApiKey),
                isLocal: false, endpoint, apiKey, payloadOptions);
            return client;
        }

        /// <summary>
        /// Creates a client whose calls go to the transport <paramref name="transportFactory"/> returns.
        /// </summary>
        /// <param name="transportFactory">Returns the transport of one call, given its access token.</param>
        /// <param name="isLocal">Whether the client behaves as an in-process one.</param>
        /// <param name="payloadOptions">The payload options; <c>null</c> creates the framework's defaults.</param>
        /// <returns>The client.</returns>
        /// <remarks>Internal: the seam exists so tests can observe and answer the calls.</remarks>
        internal static PolhemApiClient CreateWithTransport(Func<Guid, IJsonRpcTransport> transportFactory,
            bool isLocal = false, PayloadOptions? payloadOptions = null)
        {
            ArgumentNullException.ThrowIfNull(transportFactory);
            return new PolhemApiClient(transportFactory, isLocal, string.Empty, string.Empty, payloadOptions);
        }

        /// <summary>
        /// Gets whether calls run in process; <c>false</c> when they go over HTTP.
        /// </summary>
        public bool IsLocal { get; }

        /// <summary>
        /// Gets the API service endpoint; empty for an in-process client.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Gets or sets the API key sent as the <c>X-Api-Key</c> header.
        /// </summary>
        /// <remarks>
        /// Read on every request, so a new key applies from the next call. The key identifies the application, not
        /// the user, and an in-process client sends no header at all.
        /// </remarks>
        public string ApiKey
        {
            get => Volatile.Read(ref _apiKey);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Volatile.Write(ref _apiKey, value);
            }
        }

        /// <summary>
        /// Gets the signed-in state every connector of this client calls with.
        /// </summary>
        public ApiSessionContext Session { get; } = new();

        /// <summary>
        /// Gets the payload options every connector of this client reads and writes the payload envelope with.
        /// </summary>
        /// <remarks>
        /// <see cref="SystemApiConnector.InitializeAsync"/> applies the server's compressor and encryptor to this
        /// instance and leaves the rest as it is, so a setting made before it, such as
        /// <see cref="PayloadOptions.RequireFrame"/>, survives. The server must use the same frame setting.
        /// </remarks>
        public PayloadOptions PayloadOptions { get; }

        /// <summary>
        /// Gets or sets the deployment's default language, the last hop of the language fall-back chain on this
        /// client.
        /// </summary>
        /// <remarks>
        /// <see cref="SystemApiConnector.InitializeAsync"/> sets it from the server's
        /// <see cref="Polhem.Definition.Settings.CommonConfiguration.DefaultLanguage"/>. It is a deployment setting
        /// rather than a user's; the signed-in user's own culture arrives with the login response.
        /// </remarks>
        public string DefaultLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Gets the connector of the system-level actions: sign-in, company context, definitions.
        /// </summary>
        public SystemApiConnector System { get; }

        /// <summary>
        /// Gets the connector of the read-only audit-log queries.
        /// </summary>
        public AuditLogApiConnector AuditLog { get; }

        /// <summary>
        /// Creates a connector for one form.
        /// </summary>
        /// <param name="progId">The program identifier of the form.</param>
        /// <returns>A new connector; creating one is cheap and it holds no identity of its own.</returns>
        public FormApiConnector Form(string progId) => new(this, progId);

        /// <summary>
        /// Forgets the signed-in identity locally, without telling the server.
        /// </summary>
        /// <remarks>
        /// <see cref="SystemApiConnector.LogoutAsync"/> ends the session on the server and then does the same.
        /// </remarks>
        public void SignOut() => Session.SignOut();

        /// <summary>
        /// Creates the transport of one call.
        /// </summary>
        /// <param name="accessToken">The access token the call carries.</param>
        /// <returns>The transport.</returns>
        internal IJsonRpcTransport CreateTransport(Guid accessToken) => _transportFactory(accessToken);
    }
}
