using Polhem.Api.Core.Dispatch;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Client.Providers
{
    /// <summary>
    /// Local API service provider that accesses backend business logic directly within the same process.
    /// </summary>
    /// <remarks>
    /// Near-end mode needs the in-process backend's service provider, the one built by
    /// <c>services.AddPolhemFramework(...)</c>. Each call goes to its <see cref="JsonRpcDispatcher"/> through an
    /// <see cref="InProcessTransport"/>, which marks it as a local call and carries the access token in
    /// <see cref="PolhemJsonRpc.AccessTokenItem"/>.
    /// </remarks>
    internal sealed class LocalApiProvider : IJsonRpcTransport
    {
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalApiProvider"/> class.
        /// </summary>
        /// <param name="services">The in-process backend's service provider; it must register <see cref="JsonRpcDispatcher"/>.</param>
        /// <param name="accessToken">The access token.</param>
        public LocalApiProvider(IServiceProvider services, Guid accessToken)
        {
            ArgumentNullException.ThrowIfNull(services);
            _services = services;
            AccessToken = accessToken;
        }

        /// <summary>
        /// Gets the access token.
        /// </summary>
        public Guid AccessToken { get; }

        /// <summary>
        /// Sends one request to the in-process dispatcher.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The response, or <c>null</c> for a notification.</returns>
        /// <remarks>
        /// The call runs on the caller's thread, and business object methods are synchronous in
        /// 1.0 (ADR-046), so the token is observed until the method is dispatched and not while it
        /// runs.
        /// </remarks>
        public Task<JsonRpcResponse?> SendAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CreateTransport().SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Sends a batch of requests to the in-process dispatcher.
        /// </summary>
        /// <param name="requests">The requests.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The responses; notifications have none.</returns>
        public Task<IReadOnlyList<JsonRpcResponse>> SendBatchAsync(IReadOnlyList<JsonRpcRequest> requests, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CreateTransport().SendBatchAsync(requests, cancellationToken);
        }

        private InProcessTransport CreateTransport()
        {
            var dispatcher = _services.GetService(typeof(JsonRpcDispatcher)) as JsonRpcDispatcher
                ?? throw new InvalidOperationException(
                    "JsonRpcDispatcher is not registered in the service provider given to LocalApiProvider. " +
                    "Local API calls need the provider built from services.AddPolhemFramework(...).");
            var transport = new InProcessTransport(dispatcher, _services);
            transport.Items[PolhemJsonRpc.AccessTokenItem] = AccessToken;
            return transport;
        }
    }
}
