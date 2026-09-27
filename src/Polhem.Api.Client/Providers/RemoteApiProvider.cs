using System.Collections.Specialized;
using Polhem.Api.Core.JsonRpc;
using Polhem.Base.Serialization;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Client.Providers
{
    /// <summary>
    /// Remote API service provider that accesses backend business logic over the network.
    /// </summary>
    public sealed class RemoteApiProvider : IJsonRpcProvider
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteApiProvider"/> class.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        public RemoteApiProvider(string endpoint, Guid accessToken) : this(endpoint, accessToken, null)
        {
        }

        /// <summary>
        /// Initializes a new instance that sends through <paramref name="httpClient"/> instead of the
        /// shared per-host client.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="httpClient">The client to send with, or <c>null</c> for the shared per-host client.</param>
        /// <remarks>Internal: the seam exists so tests can observe the HTTP call through a fake handler.</remarks>
        internal RemoteApiProvider(string endpoint, Guid accessToken, HttpClient? httpClient)
        {
            _httpClient = httpClient;
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty.", nameof(endpoint));

            Endpoint = endpoint;
            AccessToken = accessToken;  // Note: AccessToken may be Guid.Empty for unauthenticated calls (e.g., Login, Ping)
        }

        #endregion

        private readonly HttpClient? _httpClient;

        /// <summary>
        /// Gets or sets the service endpoint.
        /// </summary>
        public string Endpoint { get; private set; }

        /// <summary>
        /// Gets the access token.
        /// </summary>
        public Guid AccessToken { get; } = Guid.Empty;

        /// <summary>
        /// Asynchronously executes an API method.
        /// </summary>
        /// <param name="request">The JSON-RPC request model.</param>
        /// <param name="cancellationToken">A token that cancels the HTTP call.</param>
        public async Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            var headers = CreateHeaders();
            string body = request.ToJson();  // Serialize input parameters to JSON
            string json = _httpClient == null
                ? await HttpUtilities.PostAsync(Endpoint, body, headers, cancellationToken).ConfigureAwait(false)
                : await HttpUtilities.PostAsync(_httpClient, Endpoint, body, headers, cancellationToken).ConfigureAwait(false);
            var response = JsonCodec.Deserialize<JsonRpcResponse>(json);  // Deserialize JSON response
            return response!;
        }

        /// <summary>
        /// Creates the HTTP header collection for the request.
        /// </summary>
        /// <remarks>
        /// The <c>Authorization</c> header is sent only when there is an access token. The server treats a request
        /// without the header as an anonymous call and leaves the decision to the method's access control, and a
        /// deployment that overrides <c>IsAuthorizationRequired</c> to refuse such requests must not be bypassed by a
        /// placeholder token.
        /// </remarks>
        private NameValueCollection CreateHeaders()
        {
            var headers = new NameValueCollection
            {
                { ApiHeaders.ApiKey, ApiClientInfo.ApiKey }
            };
            if (AccessToken != Guid.Empty)
            {
                headers.Add(ApiHeaders.Authorization, $"Bearer {AccessToken}");
            }
            return headers;
        }

    }
}
