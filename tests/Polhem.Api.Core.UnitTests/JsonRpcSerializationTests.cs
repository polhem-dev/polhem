using System.ComponentModel;
using System.Text.Json;
using Polhem.Api.Core.JsonRpc;
using Polhem.Base.Serialization;
using Polhem.Definition;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// JSON serialization tests for the JSON-RPC models, making sure the JSON wire format between front end and back end is correct.
    /// </summary>
    public class JsonRpcSerializationTests
    {
        /// <summary>
        /// Tests that JsonRpcRequest keeps all properties after serialization.
        /// </summary>
        [Fact]
        [DisplayName("JsonRpcRequest JSON serialization preserves all properties")]
        public void JsonRpcRequest_Serialize_PreservesAllProperties()
        {
            var request = new JsonRpcRequest
            {
                Jsonrpc = "2.0",
                Method = "system.ping",
                Id = "req-001"
            };
            request.Params.Value = "test-payload";
            request.Params.TypeName = "System.String";

            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<JsonRpcRequest>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("2.0", deserialized.Jsonrpc);
            Assert.Equal("system.ping", deserialized.Method);
            Assert.Equal("req-001", deserialized.Id);
            Assert.NotNull(deserialized.Params);
            Assert.Equal("test-payload", deserialized.Params.Value);
            Assert.Equal("System.String", deserialized.Params.TypeName);

            // `SerializeState` must not appear in the JSON because it is marked `[JsonIgnore]`.
            using var jDoc = JsonDocument.Parse(json);
            var root = jDoc.RootElement;
            Assert.False(root.TryGetProperty("SerializeState", out _));
            Assert.False(root.TryGetProperty("serializeState", out _));
        }

        /// <summary>
        /// Tests serialization of a successful JsonRpcResponse.
        /// </summary>
        [Fact]
        [DisplayName("JsonRpcResponse success response round-trips through JSON")]
        public void JsonRpcResponse_Success_Serialize()
        {
            var result = new JsonRpcResult
            {
                Value = "pong",
                TypeName = "System.String"
            };
            var response = new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Method = "system.ping",
                Id = "req-001",
                Result = result,
                Error = null
            };

            var json = JsonSerializer.Serialize(response);
            var deserialized = JsonSerializer.Deserialize<JsonRpcResponse>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("2.0", deserialized.Jsonrpc);
            Assert.Equal("system.ping", deserialized.Method);
            Assert.Equal("req-001", deserialized.Id);
            Assert.NotNull(deserialized.Result);
            Assert.Equal("pong", deserialized.Result.Value);
            Assert.Equal("System.String", deserialized.Result.TypeName);
            Assert.Null(deserialized.Error);
        }

        /// <summary>
        /// Tests serialization of an error JsonRpcResponse, including the numeric JsonRpcErrorCode value.
        /// </summary>
        [Fact]
        [DisplayName("JsonRpcResponse error response round-trips through JSON")]
        public void JsonRpcResponse_Error_Serialize()
        {
            var response = new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Method = "system.login",
                Id = "req-002",
                Result = null,
                Error = new JsonRpcError(
                    (int)JsonRpcErrorCode.Unauthorized,
                    "Access denied",
                    new { detail = "Invalid token" })
            };

            var json = JsonSerializer.Serialize(response);
            var deserialized = JsonSerializer.Deserialize<JsonRpcResponse>(json);

            Assert.NotNull(deserialized);
            Assert.Null(deserialized.Result);
            Assert.NotNull(deserialized.Error);
            Assert.Equal((int)JsonRpcErrorCode.Unauthorized, deserialized.Error.Code);
            Assert.Equal(-32001, deserialized.Error.Code);
            Assert.Equal("Access denied", deserialized.Error.Message);
            Assert.NotNull(deserialized.Error.Data);
        }

        /// <summary>
        /// Tests the round-trip of every JsonRpcError property.
        /// </summary>
        [Fact]
        [DisplayName("JsonRpcError JSON serialization preserves all properties")]
        public void JsonRpcError_Serialize_PreservesAllProperties()
        {
            var error = new JsonRpcError(
                (int)JsonRpcErrorCode.ParseError,
                "Parse error",
                "Unexpected token at position 42");

            var json = JsonSerializer.Serialize(error);
            var deserialized = JsonSerializer.Deserialize<JsonRpcError>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(-32700, deserialized.Code);
            Assert.Equal("Parse error", deserialized.Message);
            Assert.Equal("Unexpected token at position 42", deserialized.Data?.ToString());
        }

        /// <summary>
        /// Tests serialization of the format, value and type properties of ApiPayload (through JsonRpcParams).
        /// </summary>
        [Theory]
        [InlineData(PayloadFormat.Plain)]
        [InlineData(PayloadFormat.Encoded)]
        [InlineData(PayloadFormat.Encrypted)]
        [DisplayName("ApiPayload JSON serialization preserves Format and TypeName")]
        public void ApiPayload_Serialize_PreservesFormatAndTypeName(PayloadFormat format)
        {
            // Create payload with the specified format via JSON round-trip
            var tempJson = JsonSerializer.Serialize(new { format = (int)format, value = "sample-data", type = "Polhem.Api.Core.Messages.System.PingRequest" });
            var payload = JsonSerializer.Deserialize<JsonRpcParams>(tempJson)!;

            var json = JsonSerializer.Serialize(payload);
            var deserialized = JsonSerializer.Deserialize<JsonRpcParams>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(format, deserialized.Format);
            Assert.Equal("sample-data", deserialized.Value);
            Assert.Equal("Polhem.Api.Core.Messages.System.PingRequest", deserialized.TypeName);

            using var jDoc = JsonDocument.Parse(json);
            var root = jDoc.RootElement;
            Assert.Equal((int)format, root.GetProperty("format").GetInt32());
        }

        /// <summary>
        /// Simulates a JSON string sent by the front end and verifies that it deserializes into a JsonRpcRequest.
        /// </summary>
        [Fact]
        [DisplayName("A front-end JSON string deserializes into JsonRpcRequest")]
        public void JsonRpcRequest_FromJsonString_Deserialize()
        {
            const string json = """
                {
                    "jsonrpc": "2.0",
                    "method": "system.execFunc",
                    "params": {
                        "format": 1,
                        "value": "base64-encoded-payload",
                        "type": "Polhem.Api.Core.Messages.System.ExecFuncRequest"
                    },
                    "id": "client-req-42"
                }
                """;

            var request = JsonSerializer.Deserialize<JsonRpcRequest>(json);

            Assert.NotNull(request);
            Assert.Equal("2.0", request.Jsonrpc);
            Assert.Equal("system.execFunc", request.Method);
            Assert.Equal("client-req-42", request.Id);
            Assert.NotNull(request.Params);
            Assert.Equal(PayloadFormat.Encoded, request.Params.Format);
            Assert.Equal("base64-encoded-payload", request.Params.Value);
            Assert.Equal("Polhem.Api.Core.Messages.System.ExecFuncRequest", request.Params.TypeName);
        }

        /// <summary>
        /// Verifies that the JSON key names of a serialized JsonRpcResponse follow the JSON-RPC 2.0 specification.
        /// </summary>
        [Fact]
        [DisplayName("JsonRpcResponse serializes JSON key names that follow the JSON-RPC 2.0 specification")]
        public void JsonRpcResponse_ToJsonString_MatchesExpectedFormat()
        {
            var response = new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Method = "system.ping",
                Id = "req-100",
                Result = new JsonRpcResult
                {
                    Value = "result-data",
                    TypeName = "Polhem.Api.Core.Messages.System.PingResponse"
                }
            };

            var json = JsonSerializer.Serialize(response);
            using var jDoc = JsonDocument.Parse(json);
            var root = jDoc.RootElement;

            // The JSON-RPC 2.0 specification uses lowercase key names.
            Assert.True(root.TryGetProperty("jsonrpc", out _));
            Assert.True(root.TryGetProperty("method", out _));
            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("result", out _));

            var resultObj = root.GetProperty("result");
            Assert.True(resultObj.TryGetProperty("format", out _));
            Assert.True(resultObj.TryGetProperty("value", out _));
            Assert.True(resultObj.TryGetProperty("type", out _));

            Assert.False(root.TryGetProperty("Jsonrpc", out _));
            Assert.False(root.TryGetProperty("Method", out _));
            Assert.False(root.TryGetProperty("Id", out _));
            Assert.False(root.TryGetProperty("Result", out _));
        }

        /// <summary>
        /// Serializes the JSON-RPC request model.
        /// </summary>
        [Fact]
        [DisplayName("JsonRpcRequest serializes to valid JSON and supports encoding and decoding")]
        public void JsonRpcRequest_Serialize_ReturnsValidJson()
        {
            var request = new JsonRpcRequest()
            {
                Method = $"{SysProgIds.System}.ExecFunc",
                Params = new JsonRpcParams()
                {
                    Value = new ExecFuncRequest("Hello")
                },
                Id = Guid.NewGuid().ToString()
            };
            string json = request.ToJson();
            Assert.NotEmpty(json);

            ApiPayloadConverter.TransformTo(request.Params, PayloadFormat.Encoded);
            string encodedJson = request.ToJson();
            Assert.NotEmpty(encodedJson);

            ApiPayloadConverter.RestoreFrom(request.Params, PayloadFormat.Encoded);
            string decodedJson = request.ToJson();
            Assert.NotEmpty(decodedJson);
        }
    }
}
