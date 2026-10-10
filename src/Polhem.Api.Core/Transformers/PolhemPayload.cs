using Polhem.Api.Core.Dispatch;
using Polhem.Core.Serialization;
using Polhem.Definition.Settings;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Core.Transformers
{
    /// <summary>
    /// Builds the payload options of Polhem.JsonRpc.Payload the way the framework speaks the wire: MessagePack as the
    /// default codec, the framework's JSON spellings, its type names and allow-list, and the compressor and encryptor
    /// named by <see cref="ApiPayloadOptions"/>.
    /// </summary>
    /// <remarks>
    /// A server registers the result with <c>AddPolhemPayload</c> (Polhem.Hosting); a client keeps it in
    /// <c>PolhemApiClient.PayloadOptions</c> (Polhem.Api.Client). Both ends must agree on the compressor, the encryptor and
    /// <see cref="PayloadOptions.RequireFrame"/>, which the envelope does not name.
    /// </remarks>
    public static class PolhemPayload
    {
        /// <summary>Creates payload options with the framework's settings.</summary>
        /// <param name="settings">The compressor and encryptor names; the defaults (gzip, aes-cbc-hmac) when <see langword="null"/>.</param>
        /// <param name="isDebugMode">Whether the encryptor <c>none</c> is allowed.</param>
        /// <returns>The options.</returns>
        /// <exception cref="NotSupportedException">A compressor or encryptor name is unknown.</exception>
        /// <exception cref="InvalidOperationException">The encryptor is <c>none</c> outside debug mode.</exception>
        public static PayloadOptions CreateOptions(ApiPayloadOptions? settings = null, bool isDebugMode = false)
        {
            var options = new PayloadOptions
            {
                SerializerOptions = JsonCodec.Options,
                JsonCodec = new JsonPayloadCodec(JsonBodyOptions.Options),
                TypeResolver = PolhemPayloadTypeResolver.Instance,
            };
            options.RegisterCodec(new MessagePackPayloadCodec());
            // A payload that names no codec means MessagePack: every client that predates codec negotiation sends it
            // without naming it. This is a compatibility constant, not a preference.
            options.DefaultCodec = PayloadCodecNames.MessagePack;
            Apply(options, settings ?? new ApiPayloadOptions(), isDebugMode);
            return options;
        }

        /// <summary>
        /// Sets the compressor and the encryptor that <paramref name="settings"/> names on existing options, leaving the
        /// rest, <see cref="PayloadOptions.RequireFrame"/> included, as it was.
        /// </summary>
        /// <param name="options">The options to change.</param>
        /// <param name="settings">The compressor and encryptor names.</param>
        /// <param name="isDebugMode">Whether the encryptor <c>none</c> is allowed.</param>
        /// <exception cref="NotSupportedException">A compressor or encryptor name is unknown.</exception>
        /// <exception cref="InvalidOperationException">The encryptor is <c>none</c> outside debug mode.</exception>
        public static void Apply(PayloadOptions options, ApiPayloadOptions settings, bool isDebugMode)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(settings);

            options.Compressor = settings.Compressor switch
            {
                "gzip" => new GzipPayloadCompressor(),
                "none" or "" => NoPayloadCompressor.Instance,
                _ => throw new NotSupportedException($"Unsupported compressor: {settings.Compressor}"),
            };

            switch (settings.Encryptor)
            {
                case "aes-cbc-hmac":
                    options.Encryptor = new AesCbcHmacPayloadEncryptor();
                    options.AllowNoEncryption = false;
                    break;
                case "none" or "":
                    if (!isDebugMode)
                    {
                        throw new InvalidOperationException(
                            "The encryptor 'none' is only permitted in debug mode. Configure a valid encryptor for production.");
                    }
                    options.Encryptor = NoPayloadEncryptor.Instance;
                    options.AllowNoEncryption = true;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported encryptor: {settings.Encryptor}");
            }
        }
    }
}
