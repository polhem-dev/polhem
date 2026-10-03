using System.ComponentModel;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Api.Core.Transformers;
using Polhem.Api.Core.UnitTests.AuditLog;
using Polhem.Definition.Collections;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for the per-request body codec negotiation with the framework's payload options: JSON codec round-trips,
    /// type fidelity of `object` members, and how codec names that are not enabled or are malformed get rejected.
    /// </summary>
    public class JsonPayloadCodecTests
    {
        private static PayloadProcessor CreateProcessor() => new(PolhemPayload.CreateOptions());

        private static object? RoundTrip(object value, string codec)
        {
            var processor = CreateProcessor();
            var envelope = processor.Seal(value, PayloadFormat.Encoded, codec);
            return processor.OpenResult(envelope, null, out _);
        }

        [Fact]
        [DisplayName("An Encoded payload declaring the json codec round-trips unchanged")]
        public void Seal_JsonCodec_RoundTrips()
        {
            var processor = CreateProcessor();
            var envelope = processor.Seal(new Parameter("greeting", "hello"), PayloadFormat.Encoded, PayloadCodecNames.Json);
            Assert.NotNull(envelope.Body);
            Assert.Equal(PayloadCodecNames.Json, envelope.Codec);

            var restored = Assert.IsType<Parameter>(processor.OpenResult(envelope, null, out _));
            Assert.Equal("greeting", restored.Name);
            Assert.Equal("hello", restored.Value);
        }

        [Fact]
        [DisplayName("The DataSet of a GetChangeDetailResponse keeps row states and original values through a json codec round-trip")]
        public void Seal_JsonCodec_ChangeDetailDataSet_PreservesRowStates()
        {
            var value = new GetChangeDetailResponse
            {
                SysRowId = Guid.NewGuid(),
                DataSet = AuditLogMessagePackTests.NewChangeDataSet(),
            };

            var restored = Assert.IsType<GetChangeDetailResponse>(RoundTrip(value, PayloadCodecNames.Json));
            Assert.NotNull(restored.DataSet);
            AuditLogMessagePackTests.AssertChangeDataSet(restored.DataSet!);
        }

        [Theory]
        [DisplayName("The json codec keeps the original type of an object member instead of falling back to the default JSON mapping")]
        [InlineData(12.5)]          // decimal: a JSON number would fall back to double.
        [InlineData(9007199254740993L)] // long: above 2^53, a JSON number would lose precision.
        public void Seal_JsonCodec_PreservesObjectMemberType(object value)
        {
            // `InlineData` cannot supply a decimal directly, so the double case is converted back to decimal here.
            object original = value is double d ? (decimal)d : value;

            var restored = Assert.IsType<Parameter>(RoundTrip(new Parameter("amount", original), PayloadCodecNames.Json));
            Assert.Equal(original.GetType(), restored.Value!.GetType());
            Assert.Equal(original, restored.Value);
        }

        [Fact]
        [DisplayName("The json codec restores a Guid object member as a Guid, not a string")]
        public void Seal_JsonCodec_PreservesGuidObjectMember()
        {
            var id = Guid.NewGuid();

            var restored = Assert.IsType<Parameter>(RoundTrip(new Parameter("id", id), PayloadCodecNames.Json));
            Assert.Equal(id, Assert.IsType<Guid>(restored.Value));
        }

        [Fact]
        [DisplayName("A payload that declares no codec writes no codec field into the envelope")]
        public void Wrap_NoCodec_LeavesCodecOut()
        {
            var element = CreateProcessor().Wrap(new Parameter("greeting", "hello"), PayloadFormat.Encoded);

            Assert.False(element.TryGetProperty("codec", out _));
        }

        [Fact]
        [DisplayName("A blank codec name resolves to messagepack, the framework's compatibility default")]
        public void ResolveCodec_Blank_ReturnsMessagePack()
        {
            var options = PolhemPayload.CreateOptions();

            Assert.IsType<MessagePackPayloadCodec>(options.ResolveCodec(null));
            Assert.IsType<MessagePackPayloadCodec>(options.ResolveCodec(string.Empty));
        }

        [Fact]
        [DisplayName("The framework's options always recognize the messagepack and json codecs")]
        public void ResolveCodec_BuiltInCodecs_AlwaysResolve()
        {
            var options = PolhemPayload.CreateOptions();

            Assert.IsType<MessagePackPayloadCodec>(options.ResolveCodec(PayloadCodecNames.MessagePack));
            Assert.IsType<JsonPayloadCodec>(options.ResolveCodec(PayloadCodecNames.Json));
            Assert.Equal([PayloadCodecNames.Json, PayloadCodecNames.MessagePack], options.CodecNames.Order(StringComparer.Ordinal));
        }

        [Fact]
        [DisplayName("ResolveCodec rejects a well-formed name of a codec that does not exist")]
        public void ResolveCodec_UnknownCodec_Throws()
        {
            var ex = Assert.Throws<NotSupportedException>(() => PolhemPayload.CreateOptions().ResolveCodec("protobuf"));
            Assert.Contains("protobuf", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [DisplayName("ResolveCodec rejects a malformed codec name without echoing it")]
        [InlineData("JSON")]
        [InlineData("json; DROP")]
        [InlineData("json\nInjected: header")]
        [InlineData("../../etc/passwd")]
        public void ResolveCodec_MalformedName_ThrowsWithoutEchoing(string codec)
        {
            var ex = Assert.Throws<NotSupportedException>(() => PolhemPayload.CreateOptions().ResolveCodec(codec));

            // The name comes from the wire, so the error message must not carry it back out verbatim.
            Assert.DoesNotContain(codec, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A body encoded with messagepack but declared as json fails to decode instead of silently producing defaults")]
        public void Open_MessagePackBodyDeclaredAsJson_Fails()
        {
            var processor = CreateProcessor();
            var envelope = processor.Seal(new Parameter("greeting", "hello"), PayloadFormat.Encoded);
            var mislabeled = new PayloadEnvelope
            {
                Format = envelope.Format,
                Body = envelope.Body,
                TypeName = envelope.TypeName,
                Codec = PayloadCodecNames.Json,
            };

            Assert.ThrowsAny<Exception>(() => processor.OpenResult(mislabeled, null, out _));
        }
    }
}
