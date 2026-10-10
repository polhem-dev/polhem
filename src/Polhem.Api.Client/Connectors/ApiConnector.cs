using System.Text.Json;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Transformers;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Client;
using Polhem.JsonRpc.Payload;
using Polhem.JsonRpc.Payload.Client;

namespace Polhem.Api.Client.Connectors
{
    /// <summary>
    /// Base class for API service connectors.
    /// </summary>
    public abstract class ApiConnector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConnector"/> class.
        /// </summary>
        /// <param name="client">The client whose connection and signed-in identity this connector calls with.</param>
        protected ApiConnector(PolhemApiClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            Client = client;
        }

        /// <summary>
        /// Gets the client whose connection and signed-in identity this connector calls with.
        /// </summary>
        public PolhemApiClient Client { get; }

        private static readonly JsonRpcClientOptions s_clientOptions = new()
        {
            // Only `JsonElement` values pass through the package connector: the payload is written and read by
            // `JsonCodec`, whose settings the wire depends on.
            SerializerOptions = JsonSerializerOptions.Default,
            // GUID strings, as the requests carried before the connector was built on Polhem.JsonRpc.Client.
            IdGenerator = () => JsonRpcId.FromString(Guid.NewGuid().ToString()),
            ErrorMapper = error => MapError(error.Code, error.Message),
        };

        /// <summary>
        /// Gets or sets the body codec this connector speaks, blank for the framework default
        /// (MessagePack).
        /// </summary>
        /// <remarks>
        /// Set it to <see cref="PayloadCodecNames.Json"/> to put the bodies of this connector's
        /// calls on the JSON codec. Only the serialization step changes: compression, encryption
        /// and the anti-replay frame are unaffected, and a Plain call carries no encoded body at
        /// all, so this has no effect on one.
        /// <para>
        /// The name is stamped on the request and the server answers in the same codec. A server
        /// too old to read the field falls back to MessagePack and fails to decode the body, which
        /// is the intended outcome — a mismatch is refused rather than silently misread.
        /// </para>
        /// </remarks>
        public string PayloadCodec { get; set; } = string.Empty;

        /// <summary>
        /// Asynchronously executes an API method.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        /// <param name="action">The action name to execute.</param>
        /// <param name="value">The input parameter for the action.</param>
        /// <param name="format">The payload encoding format for transmission.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        protected async Task<T> ExecuteAsync<T>(string progId, string action, object value, PayloadFormat format,
            CancellationToken cancellationToken = default)
        {
            ValidateArgs(progId, action);
            cancellationToken.ThrowIfCancellationRequested();

            // IMPORTANT: the credentials are read once, and this call uses that one instance throughout: the token
            // it sends, the key it seals and opens with, and the zone it converts in. A sign-in on another thread
            // replaces the instance rather than its fields, so the call cannot pair one sign-in's token with
            // another's key.
            var credentials = Client.Session.Credentials;

            // The Connector is the only place time zones are applied (ADR-032 D4). A response
            // converts into the user's zone. A request converts only its filter values: a data set
            // is copied but not converted, because the server does not take DateTime values from a
            // save. The swap is undone before returning so the caller's own request object is left
            // exactly as it was handed over.
            var timeZoneId = credentials.UserTimeZoneId;

            // Guard the caller's own value before the filter conversion (ADR-032 D6). Conversion
            // rewrites filter values to Kind=Unspecified, so a guard placed after it would pass
            // every Kind=Local value on any signed-in call. It also sits ahead of every transform,
            // the one point both transports pass through with the caller's own value.
            DateTimeWireGuard.Validate(value);

            T result;
            using (PayloadZoneConverter.IsolateRequest(value, timeZoneId))
            {
                // The payload connector seals the parameters and opens the result in the same format, bound to the
                // same method (remote or local). `object` opens the result into the type its envelope names.
                var opened = await CreatePayloadConnector(credentials).InvokeAsync<object>(
                    $"{progId}.{action}", value, EffectiveFormat(format, credentials), cancellationToken).ConfigureAwait(false);

                result = FinalizeResult<T>(opened);
            }
            PayloadZoneConverter.ToUserZone(result, timeZoneId);
            return result;
        }

        /// <summary>
        /// Validates the progId and action arguments.
        /// </summary>
        private static void ValidateArgs(string progId, string action)
        {
            if (StringUtilities.IsEmpty(progId))
                throw new ArgumentException("progId cannot be null or empty.", nameof(progId));
            if (StringUtilities.IsEmpty(action))
                throw new ArgumentException("action cannot be null or empty.", nameof(action));
        }

        /// <summary>
        /// Creates the payload connector of one call.
        /// </summary>
        /// <remarks>
        /// Created per call, transport included, because the transport carries the access token of
        /// <paramref name="credentials"/>, and <see cref="PolhemApiClient.PayloadOptions"/> and
        /// <see cref="PayloadCodec"/> may change between calls. The sequence numbers come from the session, which is
        /// what keeps them per session when several connectors share it.
        /// </remarks>
        private PayloadConnector CreatePayloadConnector(ApiSessionCredentials credentials)
            => new(new JsonRpcConnector(Client.CreateTransport(credentials.AccessToken), s_clientOptions),
                new PayloadProcessor(Client.PayloadOptions), new PayloadConnectorOptions
                {
                    Codec = PayloadCodec,
                    KeyProvider = () => credentials.ApiEncryptionKey,
                    SequenceGenerator = Client.Session.NextSequence,
                });

        /// <summary>
        /// Converts the opened result value.
        /// </summary>
        private static T FinalizeResult<T>(object? opened)
        {
            var value = ResolvePlainValue(opened);
            var result = ApiOutputConverter.ConvertResultValue<T>(value!)!;
            DateTimeWireGuard.Validate(result);
            return result;
        }

        /// <summary>
        /// Reads a JSON primitive of a plain result as the .NET value it spells; objects and arrays stay
        /// <see cref="JsonElement"/> for <see cref="ApiOutputConverter.ConvertResultValue{T}(object)"/>.
        /// </summary>
        private static object? ResolvePlainValue(object? value)
        {
            if (value is not JsonElement element)
                return value;

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var number) ? number : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element,
            };
        }

        /// <summary>
        /// Turns an error response into the exception the caller sees.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Error mapping reverses what the server did on the way out, and both directions read
        /// the same declaration in <see cref="JsonRpcErrorContract"/>: a code that declares an
        /// exception type is rebuilt as that type carrying the original message with no prefix, so
        /// callers can <c>catch</c> the type instead of comparing integers. Everything else wraps
        /// into <see cref="InvalidOperationException"/> with the message
        /// <c>"API error: {code} - {message}"</c>.
        /// </para>
        /// <para>
        /// Adding a new exception type to the wire is therefore one edit, in the contract. It used
        /// to be two, in two assemblies, with nothing tying them together — which is how
        /// <see cref="JsonRpcErrorCode.ReplayRejected"/> came to be produced by the server and
        /// silently dropped into the generic branch here, leaving the <c>catch</c> that type's own
        /// documentation promises unreachable.
        /// </para>
        /// </remarks>
        private static Exception MapError(int code, string message)
            => JsonRpcErrorContract.TryRebuild(code, message, out var rebuilt)
                ? rebuilt
                : new InvalidOperationException($"API error: {code} - {message}");

        /// <summary>
        /// Returns the format this call can actually use.
        /// </summary>
        /// <param name="format">
        /// The requested format. A local client outside debug mode always sends Plain, and Encrypted falls back to
        /// Encoded while the session has no encryption key.
        /// </param>
        /// <param name="credentials">The credentials this call uses.</param>
        /// <returns>The format the call is sent in, and its result opened in.</returns>
        private PayloadFormat EffectiveFormat(PayloadFormat format, ApiSessionCredentials credentials)
        {
            // For local clients in non-debug mode, force Plain format to skip encoding/encryption and improve performance.
            if (Client.IsLocal && !SysInfo.IsDebugMode)
            {
                format = PayloadFormat.Plain; // No encoding in local non-debug mode
            }

            // If Encrypted is requested but no encryption key is set, downgrade to Encoded to prevent encryption failure.
            if (format == PayloadFormat.Encrypted && ValueUtilities.IsEmpty(credentials.ApiEncryptionKey))
            {
                format = PayloadFormat.Encoded;
            }

            return format;
        }
    }
}
