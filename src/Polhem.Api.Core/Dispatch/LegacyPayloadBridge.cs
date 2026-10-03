using Polhem.Api.Core.Transformers;
using Polhem.Core.Serialization;
using Polhem.JsonRpc.Payload;
using Polhem.JsonRpc.Payload.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Reads the payload settings of the static <see cref="ApiServiceOptions"/> into the options of the payload package,
    /// so that the server can run on the package while the client and the tests still configure the static class.
    /// </summary>
    /// <remarks>
    /// HACK: Temporary. It exists while the server has moved onto Polhem.JsonRpc.Payload and the client has not; it is
    /// removed together with <see cref="ApiServiceOptions"/>. The options are rebuilt on every call so that a change to
    /// the static settings takes effect at once, as it did before.
    /// </remarks>
    internal static class LegacyPayloadBridge
    {
        public static PayloadOptions CreateOptions()
        {
            var options = new PayloadOptions
            {
                SerializerOptions = JsonCodec.Options,
                JsonCodec = new JsonPayloadCodec(JsonPayloadSerializer.Options),
                Compressor = new CompressorAdapter(ApiServiceOptions.PayloadCompressor),
                Encryptor = new EncryptorAdapter(ApiServiceOptions.PayloadEncryptor),
                RequireFrame = ApiServiceOptions.RequireWireFrame,
                FrameTimestampTolerance = ApiServiceOptions.WireFrameTimestampTolerance,
                TypeResolver = PolhemPayloadTypeResolver.Instance,
            };
            foreach (var name in ApiServiceOptions.AcceptedPayloadCodecs)
            {
                if (name != JsonPayloadCodec.CodecName)
                    options.RegisterCodec(new CodecAdapter(ApiServiceOptions.ResolvePayloadSerializer(name)));
            }
            options.DefaultCodec = ApiServiceOptions.PayloadSerializer.SerializationMethod;
            return options;
        }

        public static IPayloadReplayStore ReplayStore { get; } = new ReplayStoreAdapter();

        private sealed class CodecAdapter(IApiPayloadSerializer serializer) : IPayloadCodec
        {
            public string Name => serializer.SerializationMethod;

            public byte[] Serialize(object value, Type type) => serializer.Serialize(value, type);

            public object? Deserialize(byte[] bytes, Type type) => serializer.Deserialize(bytes, type);
        }

        private sealed class CompressorAdapter(IApiPayloadCompressor compressor) : IPayloadCompressor
        {
            public string Name => compressor.CompressionMethod;

            public byte[] Compress(byte[] bytes) => compressor.Compress(bytes);

            public byte[] Decompress(byte[] bytes) => compressor.Decompress(bytes);
        }

        private sealed class EncryptorAdapter(IApiPayloadEncryptor encryptor) : IPayloadEncryptor
        {
            public string Name => encryptor.EncryptionMethod;

            public byte[] Encrypt(byte[] bytes, byte[] key) => encryptor.Encrypt(bytes, key);

            public byte[] Decrypt(byte[] bytes, byte[] key) => encryptor.Decrypt(bytes, key);
        }

        // The scope is the access token written by PolhemPayloadPolicy.GetReplayScope.
        private sealed class ReplayStoreAdapter : IPayloadReplayStore
        {
            public ValueTask<bool> TryAcceptAsync(string scope, long sequence, CancellationToken cancellationToken = default)
                => ApiServiceOptions.ReplayWindowStore.TryAcceptAsync(Guid.ParseExact(scope, "N"), sequence, cancellationToken);
        }
    }
}
