using Polhem.Api.Core.Messages;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Client;

namespace Polhem.Api.Client.Providers
{
    /// <summary>
    /// Remote API service provider that accesses backend business logic over the network.
    /// </summary>
    /// <remarks>
    /// Requests travel through an <see cref="HttpTransport"/>. The <c>X-Api-Key</c> and <c>Authorization</c> headers
    /// are added by a handler in front of a connection pool the process shares, so every provider has its own
    /// headers without opening connections of its own.
    /// </remarks>
    public sealed class RemoteApiProvider : IJsonRpcTransport
    {
        private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

        private readonly HttpTransport _transport;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteApiProvider"/> class.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        public RemoteApiProvider(string endpoint, Guid accessToken) : this(endpoint, accessToken, null)
        {
        }

        /// <summary>
        /// Initializes a new instance that sends through <paramref name="innerHandler"/> instead of the
        /// shared connection pool.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="innerHandler">The handler to send with, or <c>null</c> for the shared connection pool.</param>
        /// <remarks>Internal: the seam exists so tests can observe the HTTP call through a fake handler.</remarks>
        internal RemoteApiProvider(string endpoint, Guid accessToken, HttpMessageHandler? innerHandler)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty.", nameof(endpoint));

            Endpoint = endpoint;
            AccessToken = accessToken;  // Note: AccessToken may be Guid.Empty for unauthenticated calls (e.g., Login, Ping)

            var headers = new ApiHeaderHandler(accessToken)
            {
                InnerHandler = innerHandler ?? HttpUtilities.SharedHandler
            };
            // The client does not own the shared pool, and the header handler holds nothing to release, so the
            // client is never disposed.
            var client = new HttpClient(headers, disposeHandler: false) { Timeout = s_timeout };
            _transport = new HttpTransport(client, new Uri(endpoint));
        }

        /// <summary>
        /// Gets the service endpoint.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Gets the access token.
        /// </summary>
        public Guid AccessToken { get; }

        /// <summary>
        /// Sends one request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">A token that cancels the HTTP call.</param>
        /// <returns>The response, or <c>null</c> for a notification.</returns>
        public Task<JsonRpcResponse?> SendAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
            => _transport.SendAsync(request, cancellationToken);

        /// <summary>
        /// Sends a batch of requests.
        /// </summary>
        /// <param name="requests">The requests.</param>
        /// <param name="cancellationToken">A token that cancels the HTTP call.</param>
        /// <returns>The responses; notifications have none.</returns>
        public Task<IReadOnlyList<JsonRpcResponse>> SendBatchAsync(IReadOnlyList<JsonRpcRequest> requests, CancellationToken cancellationToken = default)
            => _transport.SendBatchAsync(requests, cancellationToken);

        /// <summary>
        /// Adds the API key and the access token to every request.
        /// </summary>
        /// <remarks>
        /// The API key is read per request, because <see cref="ApiClientInfo.ApiKey"/> can be set after the provider
        /// is created. The <c>Authorization</c> header is sent only when there is an access token. The server treats a
        /// request without the header as an anonymous call and leaves the decision to the method's access control,
        /// and a deployment that overrides <c>IsAuthorizationRequired</c> to refuse such requests must not be
        /// bypassed by a placeholder token.
        /// </remarks>
        private sealed class ApiHeaderHandler(Guid accessToken) : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.TryAddWithoutValidation(ApiHeaders.ApiKey, ApiClientInfo.ApiKey);
                if (accessToken != Guid.Empty)
                {
                    request.Headers.TryAddWithoutValidation(ApiHeaders.Authorization, $"Bearer {accessToken}");
                }
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
