using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.JsonRpc.Server;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// The same request through the JSON-RPC executor and through the dispatcher of Polhem.JsonRpc.Server, set up by
    /// <see cref="PolhemJsonRpc.CreateServerOptions"/>, must give the same answer. Successful answers are compared as
    /// whole JSON documents; failed ones by error code, since the two paths may word a message differently.
    /// </summary>
    /// <remarks>
    /// Both paths run in process: the executor with <c>IsLocalCall = true</c>, the dispatcher through
    /// <see cref="JsonRpcTransportKind.InProcess"/>. That is the executor's own work; the HTTP checks that ran in the
    /// controller are compared separately.
    /// </remarks>
    public class DispatcherParityTests : IClassFixture<PolhemTestFixture>
    {
        private static readonly Regex s_serverTime = new("\"serverTime\":\"[^\"]*\"", RegexOptions.None, TimeSpan.FromSeconds(1));

        private readonly PolhemTestFixture _fx;

        public DispatcherParityTests(PolhemTestFixture fx)
        {
            _fx = fx;
        }

        private async Task<string> OldAsync(string requestJson)
        {
            var request = JsonCodec.Deserialize<JsonRpcRequest>(requestJson)!;
            var executor = new JsonRpcExecutor(
                _fx.GetRequiredService<IBusinessObjectFactory>(),
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = Guid.Empty,
                IsLocalCall = true,
            };
            var response = await executor.ExecuteAsync(request);
            return response.ToJson();
        }

        private async Task<string> NewAsync(string requestJson)
        {
            var dispatcher = new JsonRpcDispatcher(PolhemJsonRpc.CreateServerOptions());
            var transport = new JsonRpcTransportInfo(
                JsonRpcTransportKind.InProcess,
                _fx.Provider,
                items: new Dictionary<string, object?> { [PolhemJsonRpc.AccessTokenItem] = Guid.Empty });
            var result = await dispatcher.DispatchMessageAsync(Encoding.UTF8.GetBytes(requestJson), transport);
            return Encoding.UTF8.GetString(result.Serialize()!);
        }

        private static string Mask(string json) => s_serverTime.Replace(json, "\"serverTime\":\"*\"");

        private static string PingRequest(PayloadFormat format, string? codec)
        {
            var payload = new JsonRpcParams { Codec = codec ?? string.Empty, Value = new PingRequest { ClientName = "parity", TraceId = "p-1" } };
            ApiPayloadConverter.TransformTo(payload, format);
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.{SystemActions.Ping}",
                Params = payload,
                Id = "parity-1",
            };
            return request.ToJson();
        }

        [Fact]
        [DisplayName("A plain Ping gets the same JSON answer from the executor and the dispatcher")]
        public async Task PlainPing_SameAnswer()
        {
            var request = PingRequest(PayloadFormat.Plain, codec: null);

            var answer = await NewAsync(request);
            Assert.Contains("\"status\":\"ok\"", answer, StringComparison.Ordinal);
            ParityAssert.SameJson(Mask(await OldAsync(request)), Mask(answer));
        }

        [Fact]
        [DisplayName("A plain request written by hand, as a JavaScript client sends it, gets the same answer")]
        public async Task HandWrittenPlainPing_SameAnswer()
        {
            const string Request = """{"jsonrpc":"2.0","method":"System.Ping","params":{"format":0,"value":{"clientName":"app","traceId":"t-1"}},"id":"1"}""";

            var answer = await NewAsync(Request);
            Assert.Contains("\"status\":\"ok\"", answer, StringComparison.Ordinal);
            ParityAssert.SameJson(Mask(await OldAsync(Request)), Mask(answer));
        }

        [Theory]
        [DisplayName("An encoded Ping gets an answer with the same envelope and the same decoded value")]
        [InlineData(PayloadCodecNames.MessagePack)]
        [InlineData(PayloadCodecNames.Json)]
        public async Task EncodedPing_SameEnvelopeAndValue(string codec)
        {
            var request = PingRequest(PayloadFormat.Encoded, codec);

            var newJson = await NewAsync(request);
            using (var answer = JsonDocument.Parse(newJson))
            {
                Assert.Equal((int)PayloadFormat.Encoded, answer.RootElement.GetProperty("result").GetProperty("format").GetInt32());
            }
            var oldResult = Result(await OldAsync(request));
            var newResult = Result(newJson);

            Assert.Equal(oldResult.Format, newResult.Format);
            Assert.Equal(oldResult.TypeName, newResult.TypeName);
            Assert.Equal(oldResult.Codec, newResult.Codec);
            var oldPong = Assert.IsType<PingResponse>(oldResult.Value);
            var newPong = Assert.IsType<PingResponse>(newResult.Value);
            Assert.Equal(oldPong.Status, newPong.Status);
            Assert.Equal(oldPong.TraceId, newPong.TraceId);
            Assert.Equal(oldPong.Version, newPong.Version);
            Assert.Equal(oldPong.ApiKeyStatus, newPong.ApiKeyStatus);
        }

        [Theory]
        [DisplayName("An unknown action or an invalid body fails with the same error code on both paths")]
        [InlineData("""{"jsonrpc":"2.0","method":"System.Nope","params":{"format":0,"value":{}},"id":"2"}""")]
        [InlineData("""{"jsonrpc":"2.0","method":"System.Ping","params":{"format":0,"value":{"clientName":5}},"id":"3"}""")]
        public async Task Failure_SameErrorCode(string request)
        {
            using var oldAnswer = JsonDocument.Parse(await OldAsync(request));
            using var newAnswer = JsonDocument.Parse(await NewAsync(request));

            Assert.Equal(
                oldAnswer.RootElement.GetProperty("error").GetProperty("code").GetInt32(),
                newAnswer.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }

        private static JsonRpcResult Result(string responseJson)
        {
            var response = JsonCodec.Deserialize<JsonRpcResponse>(responseJson)!;
            var result = response.Result!;
            ApiPayloadConverter.RestoreFrom(result, result.Format);
            return result;
        }
    }
}
