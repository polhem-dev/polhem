using System.ComponentModel;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for MessagePackPayloadCodec.
    /// </summary>
    public class MessagePackPayloadCodecTests
    {
        [Fact]
        [DisplayName("Name is \"messagepack\"")]
        public void SerializationMethod_IsMessagePack()
        {
            var serializer = new MessagePackPayloadCodec();

            Assert.Equal("messagepack", serializer.Name);
        }

        [Fact]
        [DisplayName("Serialize/Deserialize round-trips a string")]
        public void SerializeDeserialize_String_RoundTrip()
        {
            var serializer = new MessagePackPayloadCodec();
            const string original = "Hello, MessagePack!";

            var bytes = serializer.Serialize(original, typeof(string));
            var result = serializer.Deserialize(bytes, typeof(string));

            Assert.Equal(original, result);
        }

        [Fact]
        [DisplayName("Serialize/Deserialize round-trips an integer")]
        public void SerializeDeserialize_Int_RoundTrip()
        {
            var serializer = new MessagePackPayloadCodec();
            const int original = 123456;

            var bytes = serializer.Serialize(original, typeof(int));
            var result = serializer.Deserialize(bytes, typeof(int));

            Assert.Equal(original, result);
        }
    }
}
