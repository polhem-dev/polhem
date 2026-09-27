using Polhem.Base.Serialization;
using System.Text.Json.Serialization;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// JSON-RPC request model.
    /// </summary>
    public class JsonRpcRequest : IObjectSerializeBase
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcRequest"/> class.
        /// </summary>
        public JsonRpcRequest()
        { }

        #endregion

        /// <summary>
        /// Gets or sets the JSON-RPC version.
        /// </summary>
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the name of the method to invoke.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the method parameters.
        /// </summary>
        [JsonPropertyName("params")]
        public JsonRpcParams Params { get; set; } = new JsonRpcParams();

        /// <summary>
        /// Gets or sets the unique identifier for the request.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
