using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Edge cases of codec negotiation: explicitly naming the deployment default, and the accepted list matching what is actually accepted.
    /// </summary>
    [Collection(ApiServiceOptionsStateCollection.Name)]
    public class PayloadCodecNegotiationEdgeTests
    {
        /// <summary>
        /// A transformer that implements only the two-argument overloads, like every custom transformer written before negotiation existed.
        /// </summary>
        private sealed class LegacyTransformer : IApiPayloadTransformer
        {
            private readonly IApiPayloadTransformer _inner = ApiServiceOptions.PayloadTransformer;

            public byte[] Encode(object payload, Type type) => _inner.Encode(payload, type);

            public object? Decode(object payload, Type type) => _inner.Decode(payload, type);

            public byte[] Encrypt(byte[] rawBytes, byte[] encryptionKey) => _inner.Encrypt(rawBytes, encryptionKey);

            public byte[] Decrypt(byte[] encryptedBytes, byte[] encryptionKey) => _inner.Decrypt(encryptedBytes, encryptionKey);
        }

        [Fact]
        [DisplayName("Explicitly naming the deployment default codec does not require the transformer to support negotiation")]
        public void Encode_CodecNamesTheDeploymentDefault_UsesTheTwoArgumentOverload()
        {
            // This is the most natural way to write it: a client that wants to be sure which codec is used names it.
            // It changes nothing, yet it once made a custom transformer with only the two-argument overloads throw
            // `NotSupportedException`.
            var previousTransformer = ApiServiceOptions.PayloadTransformer;
            try
            {
                ApiServiceOptions.PayloadTransformer = new LegacyTransformer();
                var payload = new JsonRpcParams
                {
                    Value = new PingRequest { ClientName = "a" },
                    Codec = ApiServiceOptions.PayloadSerializer.SerializationMethod,
                };

                var exception = Record.Exception(
                    () => ApiPayloadConverter.TransformTo(payload, PayloadFormat.Encoded));

                Assert.Null(exception);

                // Control: only naming a codec that changes the body should require the new capability.
                var other = ApiServiceOptions.AcceptedPayloadCodecs
                    .First(c => !string.Equals(c, ApiServiceOptions.PayloadSerializer.SerializationMethod, StringComparison.Ordinal));
                var negotiated = new JsonRpcParams { Value = new PingRequest { ClientName = "a" }, Codec = other };

                Assert.Throws<NotSupportedException>(
                    () => ApiPayloadConverter.TransformTo(negotiated, PayloadFormat.Encoded));
            }
            finally
            {
                ApiServiceOptions.PayloadTransformer = previousTransformer;
            }
        }

        [Fact]
        [DisplayName("Every name in AcceptedPayloadCodecs resolves through ResolvePayloadSerializer, and the deployment default is listed")]
        public void AcceptedPayloadCodecs_CoversEveryNameThatResolves()
        {
            // Clients negotiate from this list. A deployment with a custom serializer was once told that the codec it
            // accepts does not exist.
            foreach (var codec in ApiServiceOptions.AcceptedPayloadCodecs)
            {
                var serializer = ApiServiceOptions.ResolvePayloadSerializer(codec);
                Assert.NotNull(serializer);
            }

            Assert.Contains(
                ApiServiceOptions.PayloadSerializer.SerializationMethod,
                ApiServiceOptions.AcceptedPayloadCodecs,
                StringComparer.Ordinal);
        }
    }
}
