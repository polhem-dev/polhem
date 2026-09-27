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
        private NameValueCollection CreateHeaders()
        {
            return new NameValueCollection
            {
                { ApiHeaders.ApiKey, ApiClientInfo.ApiKey },
                { ApiHeaders.Authorization, $"Bearer {AccessToken}" }
            };
        }

    }
}
