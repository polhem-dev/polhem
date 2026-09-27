using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Client.Providers
{
    /// <summary>
    /// Local API service provider that accesses backend business logic directly within the same process.
    /// </summary>
    /// <remarks>
    /// Near-end mode needs the in-process backend's service provider, the one built by
    /// <c>services.AddPolhemFramework(...)</c>. The provider resolves a <see cref="JsonRpcExecutor"/>
    /// per request to honour the executor's transient lifetime.
    /// </remarks>
    public class LocalApiProvider : IJsonRpcProvider
    {
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalApiProvider"/> class.
        /// </summary>
        /// <param name="services">The in-process backend's service provider; it must register <see cref="JsonRpcExecutor"/>.</param>
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
        /// Asynchronously executes an API method.
        /// </summary>
        /// <param name="request">The JSON-RPC request model.</param>
        public async Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request)
        {
            var executor = _services.GetService(typeof(JsonRpcExecutor)) as JsonRpcExecutor
                ?? throw new InvalidOperationException(
                    "JsonRpcExecutor is not registered in the service provider given to LocalApiProvider. " +
                    "Local API calls need the provider built from services.AddPolhemFramework(...).");
            executor.AccessToken = AccessToken;
            executor.IsLocalCall = true;
            return await executor.ExecuteAsync(request).ConfigureAwait(false);
        }
    }
}
