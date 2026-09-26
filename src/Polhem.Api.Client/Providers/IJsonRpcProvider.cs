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
        Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request);
    }
}
