using System.Collections.Concurrent;
using Polhem.Core;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Utility class for handling <see cref="ApiPayload"/> format conversion (serialization, compression, and encryption).
    /// </summary>
    public static class ApiPayloadConverter
    {
        // `Assembly.GetName()` builds a new `AssemblyName` on every call, and this runs on every
        // encoded or encrypted response. The keys are the wire types, a set fixed by the compiled code.
        private static readonly ConcurrentDictionary<Type, string> s_typeNames = new();

        /// <summary>
        /// Converts the specified payload object to the target format (encoded or encrypted).
        /// </summary>
        /// <param name="payload">The payload object to convert.</param>
        /// <param name="targetFormat">The target format, such as Encoded or Encrypted.</param>
        /// <param name="encryptionKey">The encryption key; required only when <paramref name="targetFormat"/> is Encrypted.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="targetFormat"/> is Encrypted but no key is provided, or when Payload.Value is null.
        /// </exception>
        /// <remarks>
        /// When <see cref="ApiServiceOptions.RequireWireFrame"/> is on, an anti-replay frame is
        /// packed in front of the encoded body. Plain payloads are returned untouched and never
        /// carry one.
        /// <para>
        /// The body codec is read from <see cref="ApiPayload.Codec"/>; leave it blank to use the
        /// deployment default, which is what every client predating negotiation does.
        /// </para>
        /// </remarks>
        public static void TransformTo(ApiPayload payload, PayloadFormat targetFormat, byte[]? encryptionKey = null)
        {
            if (targetFormat == PayloadFormat.Plain)
            {
                payload.Format = PayloadFormat.Plain;
                return;
            }

            if (payload.Value == null)
                throw new InvalidOperationException("Payload.Value cannot be null.");

            var type = payload.Value.GetType();
            payload.TypeName = s_typeNames.GetOrAdd(type, static t => t.FullName + ", " + t.Assembly.GetName().Name);

            var transformer = ApiServiceOptions.PayloadTransformer;

            // NOTE: A payload whose codec the deployment default already serves goes through the
            // two-argument overload, the one every transformer has always had. Only a codec that
            // actually changes the body asks a transformer for the newer capability, so a host's
            // own transformer keeps serving every existing client.
            //
            // WARNING: the test is "does this codec change anything", not "did the payload name
            // one". A client naming the deployment's own codec explicitly — the most natural thing
            // to write — changes nothing, and used to be handed NotSupportedException by a custom
            // transformer for asking a question with the same answer.
            byte[] bytes;
            if (UsesDefaultCodec(payload.Codec))
            {
                bytes = transformer.Encode(payload.Value, type);
            }
            else
            {
                var serializer = ApiServiceOptions.ResolvePayloadSerializer(payload.Codec);
                bytes = transformer.Encode(payload.Value, type, serializer);
            }

            if (ApiServiceOptions.RequireWireFrame)
            {
                // Prepend after encoding and before encryption, so the payload HMAC covers the frame.
                var frame = payload.Frame ?? new ApiPayloadFrame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), sequence: 0);
                payload.Frame = frame;
                bytes = frame.Prepend(bytes);
            }

            if (targetFormat == PayloadFormat.Encrypted)
            {
                if (encryptionKey == null || encryptionKey.Length == 0)
                    throw new InvalidOperationException("Encryption key is required for encrypted payload.");

                bytes = transformer.Encrypt(bytes, encryptionKey);
            }

            payload.Value = bytes;
            payload.Format = targetFormat;
        }

        /// <summary>
        /// Restores the specified payload object from its encoded or encrypted format back to the original object.
        /// </summary>
        /// <param name="payload">The payload object to restore.</param>
        /// <param name="sourceFormat">The source format; should be Encoded or Encrypted.</param>
        /// <param name="encryptionKey">The decryption key; required only when <paramref name="sourceFormat"/> is Encrypted.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="sourceFormat"/> is Encrypted but no key is provided, or when TypeName cannot be resolved.
        /// </exception>
        /// <exception cref="InvalidCastException">Thrown when Payload.Value is not of type byte[].</exception>
        /// <exception cref="ReplayRejectedException">
        /// Thrown when <see cref="ApiServiceOptions.RequireWireFrame"/> is on but the payload carries
        /// no readable frame — most often a client older than that requirement.
        /// </exception>
        /// <remarks>
        /// The frame that was read is left on <see cref="ApiPayload.Frame"/> for the caller to check.
        /// <para>
        /// The target type is resolved from <see cref="ApiPayload.TypeName"/>, after the name has
        /// passed the type allow-list. That suits a client reading a response. The server does not
        /// decode requests this way: <see cref="JsonRpcExecutor"/> decodes into the type the
        /// addressed method takes, and treats the name only as a consistency check.
        /// </para>
        /// </remarks>
        public static void RestoreFrom(ApiPayload payload, PayloadFormat sourceFormat, byte[]? encryptionKey = null)
        {
            if (sourceFormat == PayloadFormat.Plain)
            {
                payload.Format = PayloadFormat.Plain;
                return;
            }

            if (string.IsNullOrEmpty(payload.TypeName))
                throw new InvalidOperationException("TypeName is missing for deserialization.");

            // Validate TypeName against the allowed type whitelist before loading the type.
            // TypeName format: "Namespace.TypeName, AssemblyName"
            ValidateTypeName(payload.TypeName);

            var type = Type.GetType(payload.TypeName);
            if (type == null)
                throw new InvalidOperationException("Unable to load type: " + payload.TypeName);

            Decode(payload, sourceFormat, encryptionKey, type);
        }

        /// <summary>
        /// Restores a request payload into the type the server chose for it, using the name the
        /// caller wrote only to confirm the two agree.
        /// </summary>
        /// <param name="payload">The payload object to restore.</param>
        /// <param name="sourceFormat">The source format; should be Encoded or Encrypted.</param>
        /// <param name="encryptionKey">The decryption key; required only when <paramref name="sourceFormat"/> is Encrypted.</param>
        /// <param name="targetType">The type to decode into, decided by <see cref="ActionPayloadType.Resolve"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the type name is missing, is not allow-listed, or names a type other than
        /// <paramref name="targetType"/>.
        /// </exception>
        /// <remarks>
        /// The server-side counterpart of <see cref="RestoreFrom"/>, which resolves the type from the
        /// name and so suits only a reader that trusts the writer. Internal: the executor is its only
        /// caller.
        /// </remarks>
        internal static void RestoreRequest(ApiPayload payload, PayloadFormat sourceFormat, byte[]? encryptionKey, Type targetType)
        {
            if (sourceFormat == PayloadFormat.Plain)
            {
                payload.Format = PayloadFormat.Plain;
                return;
            }

            if (string.IsNullOrEmpty(payload.TypeName))
                throw new InvalidOperationException("TypeName is missing for deserialization.");

            ValidateTypeName(payload.TypeName);

            if (!ActionPayloadType.IsNamedBy(payload.TypeName, targetType))
                throw new InvalidOperationException("The payload type does not match the parameter of the requested method.");

            Decode(payload, sourceFormat, encryptionKey, targetType);
        }

        /// <summary>
        /// Decrypts when needed, strips the frame when the deployment requires one, and decodes the
        /// body into <paramref name="type"/>.
        /// </summary>
        private static void Decode(ApiPayload payload, PayloadFormat sourceFormat, byte[]? encryptionKey, Type type)
        {
            var bytes = payload.Value as byte[];
            if (bytes == null)
                throw new InvalidCastException("Payload.Value must be byte[].");

            var transformer = ApiServiceOptions.PayloadTransformer;

            if (sourceFormat == PayloadFormat.Encrypted)
            {
                if (encryptionKey == null || ValueUtilities.IsEmpty(encryptionKey))
                    throw new InvalidOperationException("Missing encryption key for encrypted payload.");

                bytes = transformer.Decrypt(bytes, encryptionKey);
            }

            if (ApiServiceOptions.RequireWireFrame)
            {
                // Whether a frame is expected is a deployment decision, never read from the packet:
                // letting a request declare "I carry no frame" would be a downgrade attack.
                payload.Frame = ApiPayloadFrame.Extract(bytes, out bytes);
            }

            // The codec is read off the payload, never passed in: the writer stamped it, and the
            // reader has to honour what actually arrived.
            payload.Value = UsesDefaultCodec(payload.Codec)
                ? transformer.Decode(bytes, type)
                : transformer.Decode(bytes, type, ApiServiceOptions.ResolvePayloadSerializer(payload.Codec));
            payload.Format = PayloadFormat.Plain;
        }

        /// <summary>
        /// Says whether the named codec is the one the deployment default already produces.
        /// </summary>
        /// <param name="codec">The codec name read off the payload envelope; blank means none was named.</param>
        /// <returns><c>true</c> when the two-argument transformer overload produces the same bytes.</returns>
        /// <remarks>
        /// Blank counts, because that is what every client predating negotiation sends. So does a
        /// codec whose name matches <see cref="ApiServiceOptions.PayloadSerializer"/>: naming the
        /// default explicitly is a legitimate thing for a client to do and must not require more of
        /// a transformer than saying nothing does.
        /// </remarks>
        private static bool UsesDefaultCodec(string? codec) =>
            string.IsNullOrEmpty(codec)
            || string.Equals(codec, ApiServiceOptions.PayloadSerializer.SerializationMethod, StringComparison.Ordinal);

        /// <summary>
        /// Validates that the TypeName is in the allowed type whitelist.
        /// Prevents arbitrary type loading from client-supplied type names.
        /// </summary>
        /// <param name="typeName">
        /// The assembly-qualified type name (e.g., "Polhem.Api.Core.Messages.System.LoginRequest, Polhem.Api.Core").
        /// </param>
        /// <exception cref="InvalidOperationException">Thrown when the type is not in the allowed whitelist.</exception>
        private static void ValidateTypeName(string typeName)
        {
            // WARNING: Screen the whole assembly-qualified name, generic arguments included. Do not
            // reduce this to "take everything before the first comma" — for a generic type that
            // comma sits inside `[[...]]`, so the fragment still starts with an allowed namespace
            // and the argument reaches `Type.GetType` unscreened.
            if (!WireTypeWhitelist.IsAssemblyQualifiedNameAllowed(typeName))
            {
                throw new InvalidOperationException(
                    $"Payload type '{typeName}' is not in the allowed type whitelist.");
            }
        }
    }

}
