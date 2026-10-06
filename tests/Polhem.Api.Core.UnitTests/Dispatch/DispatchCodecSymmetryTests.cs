using System.ComponentModel;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Definition;
using Polhem.JsonRpc.Payload;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Guards the core invariant of codec negotiation: <b>the server answers with the codec the request declared</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole mechanism (ADR-044) rests on the payload filter sealing the result with the codec the request's
    /// envelope named.
    /// Without that line, a client that negotiated json receives a MessagePack body it cannot decode, and
    /// **before 2026-09-04 the whole test suite was still green**.
    /// </para>
    /// <para>
    /// The existing <c>ApiConnectorExecuteTests</c> seems to cover this, but its fake provider writes back
    /// <c>Codec = req.Params.Codec</c> <b>itself</b>, so it proves the stub's behavior, not the server's.
    /// <c>JsonPayloadCodecTests</c> stops at the codec and never reaches the dispatcher.
    /// </para>
    /// <para>
    /// So this test asserts two things, and needs both: the response's <c>Codec</c> field equals the request's (the
    /// declaration), and the body really decodes (the content). Checking only the first would still pass if the
    /// assignment were replaced with a hard-coded string.
    /// </para>
    /// </remarks>
    public class DispatchCodecSymmetryTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public DispatchCodecSymmetryTests(PolhemTestFixture fx) { _fx = fx; }

        [Theory]
        [InlineData(PayloadCodecNames.Json)]
        [InlineData(PayloadCodecNames.MessagePack)]
        [InlineData("")]   // Undeclared: the compatibility constant (MessagePack), and the response declares nothing either.
        [DisplayName("The response codec equals the one the request declared, and the body really decodes")]
        public async Task EncodedRequest_AnswersWithTheDeclaredCodec(string codec)
        {
            var args = new PingRequest { ClientName = "codec-symmetry", TraceId = "sym-001" };
            var request = new TestRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new TestPayload { Format = PayloadFormat.Encoded, Codec = codec, Value = args },
                Id = Guid.NewGuid().ToString(),
            };

            var executor = new TestDispatcher(_fx.Provider)
            {
                AccessToken = Guid.Empty,
                IsLocalCall = true,
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<TestPayload>(response.Result);

            // Declaration: the response must report the same codec, or the client cannot know how to decode it.
            Assert.Equal(PayloadFormat.Encoded, result.Format);
            Assert.Equal(codec, result.Codec);

            // Content: the body must really be written by that codec. The test dispatcher opens it with only the codec
            // the envelope reports, so a correct declaration with the wrong content fails here instead of passing.
            var pong = Assert.IsType<PingResponse>(result.Value);
            Assert.Equal("sym-001", pong.TraceId);
        }
    }
}
