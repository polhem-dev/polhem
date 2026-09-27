using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Api.Core.Transformers;
using Polhem.Api.Core.UnitTests.AuditLog;
using Polhem.Definition.Collections;
using Polhem.Definition.Settings;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for the per-request body codec negotiation: JSON codec round-trips, type fidelity of `object` members,
    /// and how codec names that are not enabled or are malformed get rejected.
    /// </summary>
    /// <remarks>
    /// These tests rewrite the process-wide static components of <see cref="ApiServiceOptions"/>, so the class is in
    /// <c>ApiServiceOptionsState</c> (the whole assembly already uses <c>DisableTestParallelization</c>; the
    /// attribute marks that this class rewrites static components).
    /// </remarks>
    [Collection("ApiServiceOptionsState")]
    public class JsonPayloadCodecTests
    {
        /// <summary>
        /// Resets the payload pipeline to the framework defaults (messagepack / gzip / aes-cbc-hmac) and returns a
        /// disposable that restores the previous state. The json codec itself needs no enabling: both codecs are
        /// always available.
        /// </summary>
        private static Restore UseDefaultPipeline()
        {
            var originalCompressor = ApiServiceOptions.PayloadCompressor;
            var originalEncryptor = ApiServiceOptions.PayloadEncryptor;

            ApiServiceOptions.Initialize(
                new ApiPayloadOptions
                {
                    Compressor = "gzip",
                    Encryptor = "aes-cbc-hmac"
                },
                isDebugMode: true);

            return new Restore(() => ApiServiceOptions.Initialize(originalCompressor, originalEncryptor));
        }

        private sealed class Restore(Action action) : IDisposable
        {
            public void Dispose() => action();
        }

        [Fact]
        [DisplayName("An Encoded payload declaring the json codec round-trips unchanged")]
        public void TransformTo_JsonCodec_RoundTrips()
        {
            using var _ = UseDefaultPipeline();
            var payload = new JsonRpcParams
            {
                Codec = PayloadCodecNames.Json,
                Value = new Parameter("greeting", "hello")
            };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded);
            Assert.IsType<byte[]>(payload.Value);
            Assert.Equal(PayloadCodecNames.Json, payload.Codec);

            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded);

            var restored = Assert.IsType<Parameter>(payload.Value);
            Assert.Equal("greeting", restored.Name);
            Assert.Equal("hello", restored.Value);
        }

        [Fact]
        [DisplayName("The DataSet of a GetChangeDetailResponse keeps row states and original values through a json codec round-trip")]
        public void TransformTo_JsonCodec_ChangeDetailDataSet_PreservesRowStates()
        {
            using var _ = UseDefaultPipeline();
            var payload = new JsonRpcParams
            {
                Codec = PayloadCodecNames.Json,
                Value = new GetChangeDetailResponse
                {
                    SysRowId = Guid.NewGuid(),
                    DataSet = AuditLogMessagePackTests.NewChangeDataSet(),
                }
            };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded);
            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded);

            var restored = Assert.IsType<GetChangeDetailResponse>(payload.Value);
            Assert.NotNull(restored.DataSet);
            AuditLogMessagePackTests.AssertChangeDataSet(restored.DataSet!);
        }

        [Theory]
        [DisplayName("The json codec keeps the original type of an object member instead of falling back to the default JSON mapping")]
        [InlineData(12.5)]          // decimal: a JSON number would fall back to double.
        [InlineData(9007199254740993L)] // long: above 2^53, a JSON number would lose precision.
        public void TransformTo_JsonCodec_PreservesObjectMemberType(object value)
        {
            // `InlineData` cannot supply a decimal directly, so the double case is converted back to decimal here.
            object original = value is double d ? (decimal)d : value;

            using var _ = UseDefaultPipeline();
            var payload = new JsonRpcParams
            {
                Codec = PayloadCodecNames.Json,
                Value = new Parameter("amount", original)
            };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded);
            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded);

            var restored = Assert.IsType<Parameter>(payload.Value);
            Assert.Equal(original.GetType(), restored.Value!.GetType());
            Assert.Equal(original, restored.Value);
        }

        [Fact]
        [DisplayName("The json codec restores a Guid object member as a Guid, not a string")]
        public void TransformTo_JsonCodec_PreservesGuidObjectMember()
        {
            using var _ = UseDefaultPipeline();
            var id = Guid.NewGuid();
            var payload = new JsonRpcParams
            {
                Codec = PayloadCodecNames.Json,
                Value = new Parameter("id", id)
            };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded);
            ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded);

            var restored = Assert.IsType<Parameter>(payload.Value);
            Assert.Equal(id, Assert.IsType<Guid>(restored.Value));
        }

        [Fact]
        [DisplayName("A payload that declares no codec writes no codec field into the envelope")]
        public void TransformTo_NoCodec_LeavesCodecBlank()
        {
            using var _ = UseDefaultPipeline();
            var payload = new JsonRpcParams { Value = new Parameter("greeting", "hello") };

            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded);

            Assert.Equal(string.Empty, payload.Codec);
        }

        [Fact]
        [DisplayName("ResolvePayloadSerializer returns the deployment default codec for a blank name")]
        public void ResolvePayloadSerializer_Blank_ReturnsDefault()
        {
            using var _ = UseDefaultPipeline();

            Assert.Same(ApiServiceOptions.PayloadSerializer, ApiServiceOptions.ResolvePayloadSerializer(null));
            Assert.Same(ApiServiceOptions.PayloadSerializer, ApiServiceOptions.ResolvePayloadSerializer(string.Empty));
        }

        [Fact]
        [DisplayName("ResolvePayloadSerializer always recognizes the built-in codecs")]
        public void ResolvePayloadSerializer_BuiltInCodecs_AlwaysResolve()
        {
            using var _ = UseDefaultPipeline();

            Assert.IsType<MessagePackPayloadSerializer>(
                ApiServiceOptions.ResolvePayloadSerializer(PayloadCodecNames.MessagePack));
            Assert.IsType<JsonPayloadSerializer>(
                ApiServiceOptions.ResolvePayloadSerializer(PayloadCodecNames.Json));
        }

        [Fact]
        [DisplayName("ResolvePayloadSerializer rejects a well-formed name of a codec that does not exist")]
        public void ResolvePayloadSerializer_UnknownCodec_Throws()
        {
            using var _ = UseDefaultPipeline();

            var ex = Assert.Throws<NotSupportedException>(
                () => ApiServiceOptions.ResolvePayloadSerializer("protobuf"));
            Assert.Contains("protobuf", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("ResolvePayloadSerializer rejects a malformed codec name without echoing it")]
        [InlineData("JSON")]
        [InlineData("json; DROP")]
        [InlineData("json\nInjected: header")]
        [InlineData("../../etc/passwd")]
        public void ResolvePayloadSerializer_MalformedName_ThrowsWithoutEchoing(string codec)
        {
            using var _ = UseDefaultPipeline();

            var ex = Assert.Throws<NotSupportedException>(
                () => ApiServiceOptions.ResolvePayloadSerializer(codec));

            // The name comes from the wire, so the error message must not carry it back out verbatim.
            Assert.DoesNotContain(codec, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A body encoded with messagepack but declared as json fails to decode instead of silently producing defaults")]
        public void RestoreFrom_MessagePackBodyDeclaredAsJson_Fails()
        {
            using var _ = UseDefaultPipeline();
            var payload = new JsonRpcParams { Value = new Parameter("greeting", "hello") };

            // Encode with the default codec (messagepack), then claim the body is json.
            ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded);
            payload.Codec = PayloadCodecNames.Json;

            Assert.ThrowsAny<Exception>(
                () => ApiPayloadConverter.RestoreFrom(payload, PayloadFormat.Encoded));
        }
    }
}
