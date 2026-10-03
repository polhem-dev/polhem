using Polhem.Api.Core.MessagePack;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Core.Transformers
{
    /// <summary>
    /// The payload codec <c>messagepack</c>: serializes a body with the framework's MessagePack formatters. It is
    /// Polhem's default codec, the one a payload that names no codec is read with.
    /// </summary>
    public sealed class MessagePackPayloadCodec : IPayloadCodec
    {
        /// <inheritdoc/>
        public string Name => PayloadCodecNames.MessagePack;

        /// <inheritdoc/>
        public byte[] Serialize(object value, Type type) => MessagePackCodec.Serialize(value, type);

        /// <inheritdoc/>
        public object? Deserialize(byte[] bytes, Type type) => MessagePackCodec.Deserialize(bytes, type);
    }
}
