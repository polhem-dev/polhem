using Polhem.Api.Core.JsonRpc;
using Polhem.JsonRpc;

namespace Polhem.Api.Client.Providers
{
    /// <summary>
    /// Local API service provider that accesses backend business logic directly within the same process.
    /// </summary>
    /// <remarks>
    /// Near-end mode needs the in-process backend's service provider, the one built by
    /// <c>services.AddPolhemFramework(...)</c>. Each call goes through the transport its registered
    /// <see cref="IInProcessTransportFactory"/> creates, so this assembly carries none of the server side.
    /// </remarks>
    internal sealed class LocalApiProvider : IJsonRpcTransport
    {
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalApiProvider"/> class.
        /// </summary>
        /// <param name="services">The in-process backend's service provider; it must register <see cref="IInProcessTransportFactory"/>.</param>
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

        private IJsonRpcTransport CreateTransport()
        {
            var factory = _services.GetService(typeof(IInProcessTransportFactory)) as IInProcessTransportFactory
                ?? throw new InvalidOperationException(
                    "IInProcessTransportFactory is not registered in the service provider given to LocalApiProvider. " +
                    "Local API calls need the provider built from services.AddPolhemFramework(...).");
            return factory.Create(_services, AccessToken);
        }
    }
}
