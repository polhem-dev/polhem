using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Tests that <see cref="ApiConnector"/> reads a result only in the format its request was actually sent in, and
    /// reads a null result in every format.
    /// </summary>
    /// <remarks>
    /// The server answers in the format of the request, so a result in another format was written by someone else.
    /// "Actually sent" matters: the connector lowers Encrypted to Encoded while its session has no key, and the result
    /// is checked against the lowered format.
    /// </remarks>
    public class ApiConnectorResultFormatTests
    {
        private const string ProgId = "Unit";
        private const string Action = "Echo";

        private sealed class TestApiConnector(PolhemApiClient client) : ApiConnector(client)
        {
            public new Task<T> ExecuteAsync<T>(string progId, string action, object value, PayloadFormat format,
                CancellationToken cancellationToken = default)
                => base.ExecuteAsync<T>(progId, action, value, format, cancellationToken);
        }

        private static byte[] MakeKey()
        {
            var key = new byte[64];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)i;
            return key;
        }

        private static TestApiConnector CreateConnector(FakeApiTransport transport, byte[] key)
        {
            return new TestApiConnector(TestClients.Fake(transport, key: key));
        }

        /// <summary>Answers <paramref name="value"/> sealed in <paramref name="format"/>, whatever the call was sent in.</summary>
        private static FakeApiTransport AnswerIn(PayloadFormat? format, object? value, byte[] key)
            => new(call => new PayloadProcessor(TestClients.PayloadOptions).SealResponse(
                call.Method, value, format ?? call.Params.Envelope.Format, call.Params.Codec, key));

        [Theory]
        [DisplayName("A null result sealed in the format of the request reads back as null")]
        [InlineData(PayloadFormat.Plain)]
        [InlineData(PayloadFormat.Encoded)]
        [InlineData(PayloadFormat.Encrypted)]
        public async Task ExecuteAsync_NullResultInRequestFormat_ReturnsNull(PayloadFormat format)
        {
            var key = MakeKey();
            var transport = AnswerIn(null, null, key);
            var connector = CreateConnector(transport, key);

            var result = await connector.ExecuteAsync<PingResponse?>(ProgId, Action, "payload", format);

            Assert.Null(result);
            Assert.Equal(format, transport.LastCall!.Params.Format);
        }

        [Theory]
        [DisplayName("A result in a format other than the one the request was sent in is refused")]
        [InlineData(PayloadFormat.Encrypted, PayloadFormat.Plain)]
        [InlineData(PayloadFormat.Encrypted, PayloadFormat.Encoded)]
        [InlineData(PayloadFormat.Encoded, PayloadFormat.Plain)]
        public async Task ExecuteAsync_ResultInAnotherFormat_Throws(PayloadFormat sent, PayloadFormat answered)
        {
            var key = MakeKey();
            var connector = CreateConnector(AnswerIn(answered, "swapped", key), key);

            await Assert.ThrowsAsync<InvalidPayloadException>(() =>
                connector.ExecuteAsync<string>(ProgId, Action, "payload", sent));
        }

        [Fact]
        [DisplayName("A request lowered from Encrypted to Encoded for want of a key accepts an Encoded result")]
        public async Task ExecuteAsync_EncryptedLoweredToEncoded_AcceptsEncodedResult()
        {
            var transport = AnswerIn(null, "echoed", []);
            var connector = CreateConnector(transport, []);

            var result = await connector.ExecuteAsync<string>(ProgId, Action, "payload", PayloadFormat.Encrypted);

            Assert.Equal("echoed", result);
            Assert.Equal(PayloadFormat.Encoded, transport.LastCall!.Params.Format);
        }
    }
}
