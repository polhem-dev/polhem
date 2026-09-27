using System.Text.Json.Serialization;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Represents the input parameters for a JSON-RPC method invocation.
    /// </summary>
    [JsonConverter(typeof(ApiPayloadJsonConverter<JsonRpcParams>))]
    public sealed class JsonRpcParams : ApiPayload
    {
    }
}
