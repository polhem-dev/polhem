using Polhem.Base.Serialization;
using System.Text.Json.Serialization;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// JSON-RPC response model.
    /// </summary>
    public sealed class JsonRpcResponse : IObjectSerializeBase
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcResponse"/> class.
        /// </summary>
        public JsonRpcResponse()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcResponse"/> class based on the specified request.
        /// </summary>
        /// <param name="request">The JSON-RPC request model.</param>
        public JsonRpcResponse(JsonRpcRequest request)
        {
            Method = request.Method; // Echo back the invoked method name
            Id = request.Id;  // Set the unique identifier from the request
        }

        #endregion

        /// <summary>
        /// Gets or sets the JSON-RPC version.
        /// </summary>
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the name of the invoked method.
        /// </summary>
        [JsonPropertyName("method")]
        public string? Method { get; set; }

        /// <summary>
        /// Gets or sets the method execution result.
        /// </summary>
        [JsonPropertyName("result")]
        public JsonRpcResult? Result { get; set; }

        /// <summary>
        /// Gets or sets the error information.
        /// </summary>
        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the request.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
