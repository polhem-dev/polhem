using System.Text.Json.Serialization;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Represents the return result of a JSON-RPC method invocation.
    /// </summary>
    [JsonConverter(typeof(ApiPayloadJsonConverter<JsonRpcResult>))]
    public sealed class JsonRpcResult : ApiPayload
    {
    }
}
