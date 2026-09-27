using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Definition;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Guards the core invariant of codec negotiation: <b>the server answers with the codec the request declared</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole mechanism (ADR-044) rests on one assignment in <c>JsonRpcExecutor</c>:
    /// <c>response.Result = new JsonRpcResult { Value = value, Codec = request.Params.Codec }</c>.
    /// Without that line, a client that negotiated json receives a MessagePack body it cannot decode, and
    /// **before 2026-09-04 the whole test suite was still green**.
    /// </para>
    /// <para>
    /// The existing <c>ApiConnectorExecuteTests</c> seems to cover this, but its fake provider writes back
    /// <c>Codec = req.Params.Codec</c> <b>itself</b>, so it proves the stub's behavior, not the executor's.
    /// <c>JsonPayloadCodecTests</c> stops at the <c>ApiPayloadConverter</c> layer and never reaches the executor.
    /// </para>
    /// <para>
    /// So this test asserts two things, and needs both: the response's <c>Codec</c> field equals the request's (the
    /// declaration), and the body really decodes (the content). Checking only the first would still pass if the
    /// assignment were replaced with a hard-coded string.
    /// </para>
    /// </remarks>
    [Collection("ApiServiceOptionsState")]
    public class JsonRpcExecutorCodecSymmetryTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public JsonRpcExecutorCodecSymmetryTests(PolhemTestFixture fx) { _fx = fx; }

        /// <summary>
        /// Resets the payload pipeline to the framework defaults and returns a disposable that restores the previous state.
        /// </summary>
        private static Restore UseDefaultPipeline()
        {
            var compressor = ApiServiceOptions.PayloadCompressor;
            var encryptor = ApiServiceOptions.PayloadEncryptor;

            ApiServiceOptions.Initialize(
                new ApiPayloadOptions { Compressor = "gzip", Encryptor = "aes-cbc-hmac" },
                isDebugMode: true);

            return new Restore(() => ApiServiceOptions.Initialize(compressor, encryptor));
        }

        private sealed class Restore(Action action) : IDisposable
        {
            public void Dispose() => action();
        }

        [Theory]
        [InlineData(PayloadCodecNames.Json)]
        [InlineData(PayloadCodecNames.MessagePack)]
        [InlineData("")]   // Undeclared: the compatibility constant (MessagePack), and the response declares nothing either.
        [DisplayName("The response codec equals the one the request declared, and the body really decodes")]
        public async Task Execute_EncodedRequest_AnswersWithTheDeclaredCodec(string codec)
        {
            using var _ = UseDefaultPipeline();

            var args = new PingRequest { ClientName = "codec-symmetry", TraceId = "sym-001" };
            var request = new JsonRpcRequest
            {
                Method = $"{SysProgIds.System}.Ping",
                Params = new JsonRpcParams { Codec = codec, Value = args },
                Id = Guid.NewGuid().ToString(),
            };
            ApiPayloadConverter.TransformTo(request.Params, PayloadFormat.Encoded);

            var executor = new JsonRpcExecutor(
                _fx.GetRequiredService<IBusinessObjectFactory>(),
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = Guid.Empty,
                IsLocalCall = true,
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<JsonRpcResult>(response.Result);

            // Declaration: the response must report the same codec, or the client cannot know how to decode it.
            Assert.Equal(codec, result.Codec);

            // Content: the body must really be written by that codec. `RestoreFrom` reads only the codec the payload
            // reports, so a correct declaration with the wrong content fails here instead of passing silently.
            Assert.IsType<byte[]>(result.Value);
            ApiPayloadConverter.RestoreFrom(result, PayloadFormat.Encoded);

            var pong = Assert.IsType<PingResponse>(result.Value);
            Assert.Equal("sym-001", pong.TraceId);
        }
    }
}
