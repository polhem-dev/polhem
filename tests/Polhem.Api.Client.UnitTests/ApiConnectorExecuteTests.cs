using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Transformers;
using Polhem.JsonRpc.Payload;
using Polhem.JsonRpc;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for <c>ExecuteAsync</c> of <see cref="ApiConnector"/> and its helper flow.
    /// <see cref="FakeApiTransport"/> replaces the real transport, so no external service is needed.
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
            public TestApiConnector(PolhemApiClient client) : base(client) { }

            public new Task<T> ExecuteAsync<T>(string progId, string action, object value, PayloadFormat format,
                CancellationToken cancellationToken = default)
                => base.ExecuteAsync<T>(progId, action, value, format, cancellationToken);
        }

        /// <summary>
        /// Creates a connector whose calls go to the test transport.
        /// </summary>
        private static TestApiConnector CreateConnector(FakeApiTransport transport)
            => new(TestClients.Fake(transport));

        [Fact]
        [DisplayName("ExecuteAsync returns the provider result converted to the target type on success")]
        public async Task ExecuteAsync_Plain_ReturnsConvertedResult()
        {
            var transport = new FakeApiTransport();
            var connector = CreateConnector(transport);

            var result = await connector.ExecuteAsync<string>(TestProgId, TestAction, new object(), PayloadFormat.Plain);

            Assert.Equal("ok", result);
            var call = Assert.Single(transport.Calls);
            Assert.Equal($"{TestProgId}.{TestAction}", call.Method);
            Assert.Equal(JsonRpcIdKind.String, call.Id.Kind);
            Assert.True(Guid.TryParse(call.Id.StringValue, out _));
        }

        [Fact]
        [DisplayName("ExecuteAsync throws InvalidOperationException when the provider returns an Error")]
        public async Task ExecuteAsync_WithErrorResponse_ThrowsInvalidOperationException()
        {
            var connector = CreateConnector(new FakeApiTransport(_ => throw new JsonRpcErrorException(-32601, "Method not found")));

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
            var connector = CreateConnector(new FakeApiTransport());
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await connector.ExecuteAsync<object>(progId!, TestAction, new object(), PayloadFormat.Plain));
        }

        #region PayloadCodec

        /// <summary>
        /// Answers in the codec and format the request declared, as the server does. Without this step the client
        /// gets a Result without a TypeName and fails while restoring it, which is a problem of the test scaffolding,
        /// not the behavior under test.
        /// </summary>
        private static FakeApiTransport CreateEchoTransport()
            => new(call => FakeApiTransport.Answer(call, "echoed"));

        [Theory]
        [DisplayName("With PayloadCodec set, a non-Plain request stamps that codec on the envelope")]
        [InlineData(PayloadFormat.Encoded)]
        [InlineData(PayloadFormat.Encrypted)]
        public async Task ExecuteAsync_WithPayloadCodec_StampsCodecOnRequest(PayloadFormat format)
        {
            var transport = CreateEchoTransport();
            var connector = CreateConnector(transport);
            connector.PayloadCodec = PayloadCodecNames.Json;

            // Without a key, Encrypted degrades to Encoded automatically. Both encode the body, which is what this
            // test looks at.
            await connector.ExecuteAsync<string>(TestProgId, TestAction, "payload", format);

            Assert.Equal(PayloadCodecNames.Json, transport.LastCall!.Params.Codec);
            Assert.IsType<byte[]>(transport.LastCall.Params.Value);
        }

        [Fact]
        [DisplayName("Without PayloadCodec the envelope codec stays blank, keeping the existing MessagePack behavior")]
        public async Task ExecuteAsync_WithoutPayloadCodec_LeavesCodecBlank()
        {
            var transport = CreateEchoTransport();
            var connector = CreateConnector(transport);

            await connector.ExecuteAsync<string>(TestProgId, TestAction, "payload", PayloadFormat.Encoded);

            Assert.Equal(string.Empty, transport.LastCall!.Params.Codec);
        }

        [Fact]
        [DisplayName("A Plain request carries no encoded body, so it does not stamp a codec")]
        public async Task ExecuteAsync_PlainFormat_DoesNotStampCodec()
        {
            var transport = CreateEchoTransport();
            var connector = CreateConnector(transport);
            connector.PayloadCodec = PayloadCodecNames.Json;

            await connector.ExecuteAsync<string>(TestProgId, TestAction, "payload", PayloadFormat.Plain);

            Assert.Equal(string.Empty, transport.LastCall!.Params.Codec);
        }

        [Fact]
        [DisplayName("A request sent with the json codec decodes a response encoded with the json codec")]
        public async Task ExecuteAsync_JsonCodec_RoundTripsThroughResponse()
        {
            var transport = CreateEchoTransport();
            var connector = CreateConnector(transport);
            connector.PayloadCodec = PayloadCodecNames.Json;

            var result = await connector.ExecuteAsync<string>(
                TestProgId, TestAction, "payload", PayloadFormat.Encoded);

            Assert.Equal("echoed", result);
        }

        #endregion
    }
}
