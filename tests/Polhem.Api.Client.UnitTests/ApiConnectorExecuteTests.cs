using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client.Providers;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for <c>ExecuteAsync</c> of <see cref="ApiConnector"/> and its helper flow.
    /// <see cref="FakeJsonRpcProvider"/> replaces the real JSON-RPC provider, so no external service is needed.
    /// </summary>
    public class ApiConnectorExecuteTests
    {
        private const string TestProgId = "Unit";
        private const string TestAction = "Echo";

        /// <summary>
        /// Exposes <c>ExecuteAsync</c> of <see cref="ApiConnector"/> so the tests can call it directly.
        /// </summary>
        private sealed class TestApiConnector : ApiConnector
        {
            public TestApiConnector(Guid accessToken) : base(accessToken) { }

            public new Task<T> ExecuteAsync<T>(string progId, string action, object value, PayloadFormat format)
                => base.ExecuteAsync<T>(progId, action, value, format);
        }

        /// <summary>
        /// A fake <see cref="IJsonRpcProvider"/> with a customizable response, for unit tests only.
        /// </summary>
        private sealed class FakeJsonRpcProvider : IJsonRpcProvider
        {
            public JsonRpcRequest? LastRequest { get; private set; }
            public int AsyncCallCount { get; private set; }
            public Func<JsonRpcRequest, JsonRpcResponse> ResponseFactory { get; set; }
                = req => new JsonRpcResponse(req)
                {
                    Result = new JsonRpcResult { Value = "ok" }
                };

            public Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request)
            {
                LastRequest = request;
                AsyncCallCount++;
                return Task.FromResult(ResponseFactory(request));
            }
        }

        /// <summary>
        /// Replaces <see cref="ApiConnector.Provider"/> (private setter) with the test provider through reflection.
        /// </summary>
        private static void InjectProvider(ApiConnector connector, IJsonRpcProvider provider)
        {
            var prop = typeof(ApiConnector).GetProperty(nameof(ApiConnector.Provider),
                BindingFlags.Public | BindingFlags.Instance)!;
            prop.SetValue(connector, provider);
        }

        private static TestApiConnector CreateConnector(FakeJsonRpcProvider provider)
        {
            var connector = new TestApiConnector(Guid.NewGuid());
            InjectProvider(connector, provider);
            return connector;
        }

        [Fact]
        [DisplayName("ExecuteAsync returns the provider result converted to the target type on success")]
        public async Task ExecuteAsync_Plain_ReturnsConvertedResult()
        {
            var provider = new FakeJsonRpcProvider();
            var connector = CreateConnector(provider);

            var result = await connector.ExecuteAsync<string>(TestProgId, TestAction, new object(), PayloadFormat.Plain);

            Assert.Equal("ok", result);
            Assert.Equal(1, provider.AsyncCallCount);
            Assert.NotNull(provider.LastRequest);
            Assert.Equal($"{TestProgId}.{TestAction}", provider.LastRequest!.Method);
            Assert.False(string.IsNullOrEmpty(provider.LastRequest.Id));
            Assert.NotNull(provider.LastRequest.Params);
        }

        [Fact]
        [DisplayName("ExecuteAsync throws InvalidOperationException when the provider returns an Error")]
        public async Task ExecuteAsync_WithErrorResponse_ThrowsInvalidOperationException()
        {
            var provider = new FakeJsonRpcProvider
            {
                ResponseFactory = req => new JsonRpcResponse(req)
                {
                    Error = new JsonRpcError(-32601, "Method not found")
                }
            };
            var connector = CreateConnector(provider);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await connector.ExecuteAsync<string>(TestProgId, TestAction, new object(), PayloadFormat.Plain));

            Assert.Contains("-32601", ex.Message);
            Assert.Contains("Method not found", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [DisplayName("ExecuteAsync throws ArgumentException for an empty progId")]
        public async Task ExecuteAsync_EmptyProgId_ThrowsArgumentException(string? progId)
        {
            var connector = CreateConnector(new FakeJsonRpcProvider());
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await connector.ExecuteAsync<object>(progId!, TestAction, new object(), PayloadFormat.Plain));
        }

        #region PayloadCodec

        /// <summary>
        /// Simulates the server's response: it encodes the response with the codec and format the request declared,
        /// which is what <c>JsonRpcExecutor</c> does. Without this step the client gets a Result without a TypeName
        /// and fails while restoring it, which is a problem of the test scaffolding, not the behavior under test.
        /// </summary>
        private static FakeJsonRpcProvider CreateEchoProvider(PayloadFormat format)
        {
            return new FakeJsonRpcProvider
            {
                ResponseFactory = req =>
                {
                    var response = new JsonRpcResponse(req)
                    {
                        Result = new JsonRpcResult { Value = "echoed", Codec = req.Params.Codec }
                    };
                    // Without a key the client degrades Encrypted to Encoded, so the response uses the same format.
                    var actual = format == PayloadFormat.Plain ? PayloadFormat.Plain : PayloadFormat.Encoded;
                    ApiPayloadConverter.TransformTo(response.Result, actual);
                    return response;
                }
            };
        }

        [Theory]
        [DisplayName("With PayloadCodec set, a non-Plain request stamps that codec on the envelope")]
        [InlineData(PayloadFormat.Encoded)]
        [InlineData(PayloadFormat.Encrypted)]
        public async Task ExecuteAsync_WithPayloadCodec_StampsCodecOnRequest(PayloadFormat format)
        {
            var provider = CreateEchoProvider(format);
            var connector = CreateConnector(provider);
            connector.PayloadCodec = PayloadCodecNames.Json;

            // Without a key, Encrypted degrades to Encoded automatically. Both encode the body, which is what this
            // test looks at.
            await connector.ExecuteAsync<string>(TestProgId, TestAction, "payload", format);

            Assert.Equal(PayloadCodecNames.Json, provider.LastRequest!.Params.Codec);
            Assert.IsType<byte[]>(provider.LastRequest.Params.Value);
        }

        [Fact]
        [DisplayName("Without PayloadCodec the envelope codec stays blank, keeping the existing MessagePack behavior")]
        public async Task ExecuteAsync_WithoutPayloadCodec_LeavesCodecBlank()
        {
            var provider = CreateEchoProvider(PayloadFormat.Encoded);
            var connector = CreateConnector(provider);

            await connector.ExecuteAsync<string>(TestProgId, TestAction, "payload", PayloadFormat.Encoded);

            Assert.Equal(string.Empty, provider.LastRequest!.Params.Codec);
        }

        [Fact]
        [DisplayName("A Plain request carries no encoded body, so it does not stamp a codec")]
        public async Task ExecuteAsync_PlainFormat_DoesNotStampCodec()
        {
            var provider = CreateEchoProvider(PayloadFormat.Plain);
            var connector = CreateConnector(provider);
            connector.PayloadCodec = PayloadCodecNames.Json;

            await connector.ExecuteAsync<string>(TestProgId, TestAction, "payload", PayloadFormat.Plain);

            Assert.Equal(string.Empty, provider.LastRequest!.Params.Codec);
        }

        [Fact]
        [DisplayName("A request sent with the json codec decodes a response encoded with the json codec")]
        public async Task ExecuteAsync_JsonCodec_RoundTripsThroughResponse()
        {
            var provider = CreateEchoProvider(PayloadFormat.Encoded);
            var connector = CreateConnector(provider);
            connector.PayloadCodec = PayloadCodecNames.Json;

            var result = await connector.ExecuteAsync<string>(
                TestProgId, TestAction, "payload", PayloadFormat.Encoded);

            Assert.Equal("echoed", result);
        }

        #endregion
    }
}
