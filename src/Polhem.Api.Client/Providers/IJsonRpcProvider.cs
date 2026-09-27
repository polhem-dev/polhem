using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Client.Providers
{
    /// <summary>
    /// Interface for a JSON-RPC service provider.
    /// </summary>
    public interface IJsonRpcProvider
    {
        /// <summary>
        /// Asynchronously executes an API method.
        /// </summary>
        /// <param name="request">The JSON-RPC request model.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request, CancellationToken cancellationToken = default);
    }
}
