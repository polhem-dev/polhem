using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Transformers;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Client;

namespace Polhem.Api.Client.Connectors
{
    /// <summary>
    /// Base class for API service connectors.
    /// </summary>
    public abstract class ApiConnector
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConnector"/> class using a local connection.
        /// </summary>
        /// <param name="services">The in-process backend's service provider, built by <c>services.AddPolhemFramework(...)</c>.</param>
        /// <param name="accessToken">The access token.</param>
        protected ApiConnector(IServiceProvider services, Guid accessToken)
            : this(services, accessToken, ApiSessionContext.Ambient)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConnector"/> class using a local connection
        /// and the given session state.
        /// </summary>
        /// <param name="services">The in-process backend's service provider, built by <c>services.AddPolhemFramework(...)</c>.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="session">
        /// The per-session state. A host serving several users from one process must give each session
        /// its own instance; sharing one makes the last login's transmission key overwrite the rest.
        /// </param>
        protected ApiConnector(IServiceProvider services, Guid accessToken, ApiSessionContext session)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(session);
            AccessToken = accessToken;
            Session = session;
            SetProvider(new LocalApiProvider(services, accessToken));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConnector"/> class using a remote connection.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        protected ApiConnector(string endpoint, Guid accessToken)
            : this(endpoint, accessToken, ApiSessionContext.Ambient)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConnector"/> class using a remote connection
        /// and the given session state.
        /// </summary>
        /// <param name="endpoint">The API service endpoint.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="session">
        /// The per-session state. This is the overload a multi-user host wants: the remote path is the
        /// one that encrypts payloads, so a shared context there is what locks users out of each other's
        /// sessions.
        /// </param>
        protected ApiConnector(string endpoint, Guid accessToken, ApiSessionContext session)
        {
            if (StringUtilities.IsEmpty(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty.", nameof(endpoint));
            ArgumentNullException.ThrowIfNull(session);

            AccessToken = accessToken;
            Session = session;
            SetProvider(new RemoteApiProvider(endpoint, accessToken));
        }

        #endregion

        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        public Guid AccessToken { get; private set; }

        /// <summary>
        /// Gets the per-session state this connector reads and writes.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="ApiSessionContext.Ambient"/> for connectors created through the
        /// constructors that do not take one, which is what keeps single-user hosts unchanged.
        /// </remarks>
        public ApiSessionContext Session { get; } = ApiSessionContext.Ambient;

        private static readonly JsonRpcClientOptions s_clientOptions = new()
        {
            // Only `JsonElement` values pass through the package connector: the payload is written and read by
            // `JsonCodec`, whose settings the wire depends on.
            SerializerOptions = JsonSerializerOptions.Default,
            // GUID strings, as the requests carried before the connector was built on Polhem.JsonRpc.Client.
            IdGenerator = () => JsonRpcId.FromString(Guid.NewGuid().ToString()),
            ErrorMapper = error => MapError(error.Code, error.Message),
        };

        private IJsonRpcTransport _provider;
        private JsonRpcConnector _connector;

        /// <summary>
        /// Gets the transport this connector's calls go through: a <see cref="LocalApiProvider"/> or a
        /// <see cref="RemoteApiProvider"/>.
        /// </summary>
        public IJsonRpcTransport Provider
        {
            get => _provider;
            private set => SetProvider(value);
        }

        [MemberNotNull(nameof(_provider), nameof(_connector))]
        private void SetProvider(IJsonRpcTransport provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            _provider = provider;
            _connector = new JsonRpcConnector(provider, s_clientOptions);
        }

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

            // The Connector is the only place time zones are applied (ADR-032 D4). A response
            // converts into the user's zone. A request converts only its filter values: a data set
            // is copied but not converted, because the server does not take DateTime values from a
            // save. The swap is undone before returning so the caller's own request object is left
            // exactly as it was handed over.
            var timeZoneId = UserTimeZoneId;

            // Guard the caller's own value before the filter conversion (ADR-032 D6). Conversion
            // rewrites filter values to Kind=Unspecified, so a guard placed after it would pass
            // every Kind=Local value on any signed-in call. It also sits ahead of every transform,
            // the one point both transports pass through with the caller's own value.
            DateTimeWireGuard.Validate(value);

            T result;
            using (PayloadZoneConverter.IsolateRequest(value, timeZoneId))
            {
                var payload = new Polhem.JsonRpc.Payload.PayloadProcessor(LegacyPayloadBridge.CreateOptions());
                var parameters = WrapRequest(payload, value, format);

                // Invoke the JSON-RPC method (remote or local)
                var element = await _connector.InvokeAsync<JsonElement>(
                    $"{progId}.{action}", parameters, cancellationToken).ConfigureAwait(false);

                result = FinalizeResult<T>(payload, element);
            }
            PayloadZoneConverter.ToUserZone(result, timeZoneId);
            return result;
        }

        /// <summary>
        /// The signed-in user's IANA time zone id, or blank (meaning no conversion) before login.
        /// </summary>
        /// <remarks>
        /// Read per call rather than captured: a connector instance outlives a sign-in, and a stale
        /// zone would silently shift another user's data (ADR-032 D13).
        /// </remarks>
        private string UserTimeZoneId => Session.UserTimeZoneId;

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
        /// Opens the result payload and converts the result value.
        /// </summary>
        private T FinalizeResult<T>(Polhem.JsonRpc.Payload.PayloadProcessor payload, JsonElement element)
        {
            if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                throw new InvalidOperationException("The API answered without a result.");

            var value = ResolvePlainValue(payload.Unwrap(element, Session.ApiEncryptionKey));
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
        /// Wraps the request value in the payload envelope, in the format this call can actually use.
        /// </summary>
        /// <param name="payload">The payload processor of the call.</param>
        /// <param name="value">The request value.</param>
        /// <param name="format">
        /// The requested format. A local provider outside debug mode always sends Plain, and Encrypted falls back to
        /// Encoded while the session has no encryption key.
        /// </param>
        /// <returns>The <c>params</c> element.</returns>
        private JsonElement WrapRequest(Polhem.JsonRpc.Payload.PayloadProcessor payload, object value, PayloadFormat format)
        {
            // For local providers in non-debug mode, force Plain format to skip encoding/encryption and improve performance.
            if (this.Provider is LocalApiProvider && !SysInfo.IsDebugMode)
            {
                format = PayloadFormat.Plain; // No encoding in local non-debug mode
            }

            // If Encrypted is requested but no encryption key is set, downgrade to Encoded to prevent encryption failure.
            if (format == PayloadFormat.Encrypted && ValueUtilities.IsEmpty(Session.ApiEncryptionKey))
            {
                format = PayloadFormat.Encoded;
            }

            if (format == PayloadFormat.Plain)
                return payload.Wrap(value, Polhem.JsonRpc.Payload.PayloadFormat.Plain);

            // Numbered here: only the connector knows which session the call belongs to, and the counter is per
            // session. A number is spent only when frames are on, as before.
            long sequence = payload.Options.RequireFrame ? Session.NextSequence() : 0;
            return payload.Wrap(value, (Polhem.JsonRpc.Payload.PayloadFormat)format, PayloadCodec,
                Session.ApiEncryptionKey, sequence);
        }
    }
}
