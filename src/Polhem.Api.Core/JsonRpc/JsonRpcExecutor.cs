using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Polhem.Base;
using Polhem.Base.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Logging;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Api.Core.Validator;
using Polhem.Api.Core.Conversion;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// JSON-RPC request executor.
    /// </summary>
    public partial class JsonRpcExecutor
    {
        private static readonly char[] s_methodSeparators = new[] { '.' };

        private readonly IBusinessObjectFactory _boFactory;
        private readonly IAccessTokenValidator _tokenValidator;
        private readonly IApiEncryptionKeyProvider _keyProvider;
        private readonly IAnomalyLogWriter? _anomalyWriter;
        private readonly AuditLogOptions? _auditOptions;
        private readonly ISessionInfoService? _sessionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcExecutor"/> class without API anomaly
        /// logging.
        /// </summary>
        /// <param name="boFactory">The business-object factory.</param>
        /// <param name="tokenValidator">The access-token validator.</param>
        /// <param name="keyProvider">The API encryption key provider.</param>
        public JsonRpcExecutor(
            IBusinessObjectFactory boFactory,
            IAccessTokenValidator tokenValidator,
            IApiEncryptionKeyProvider keyProvider)
            : this(boFactory, tokenValidator, keyProvider, anomalyWriter: null, auditOptions: null, sessionService: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcExecutor"/> class.
        /// </summary>
        /// <param name="boFactory">The business-object factory.</param>
        /// <param name="tokenValidator">The access-token validator.</param>
        /// <param name="keyProvider">The API encryption key provider.</param>
        /// <param name="anomalyWriter">The writer for API anomaly records; null disables API anomaly logging.</param>
        /// <param name="auditOptions">The audit-log options (anomaly enable + API slow threshold), or null.</param>
        /// <param name="sessionService">The session lookup for the acting user (denormalised who), or null.</param>
        public JsonRpcExecutor(
            IBusinessObjectFactory boFactory,
            IAccessTokenValidator tokenValidator,
            IApiEncryptionKeyProvider keyProvider,
            IAnomalyLogWriter? anomalyWriter,
            AuditLogOptions? auditOptions,
            ISessionInfoService? sessionService)
        {
            _boFactory = boFactory ?? throw new ArgumentNullException(nameof(boFactory));
            _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _anomalyWriter = anomalyWriter;
            _auditOptions = auditOptions;
            _sessionService = sessionService;
        }

        /// <summary>
        /// Gets or sets the access token used to identify the current user or session.
        /// </summary>
        public Guid AccessToken { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the call originates from a local source (e.g., the same process or host as the server).
        /// </summary>
        public bool IsLocalCall { get; set; } = false;

        /// <summary>
        /// Gets or sets the logger that records the real message of a failure whose caller is only
        /// given a generic one.
        /// </summary>
        /// <remarks>
        /// <c>AddPolhemFramework</c> assigns it. Left null, failures still reach the caller with the
        /// generic message and nothing is logged here.
        /// </remarks>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Gets or sets the API key verdict for the current call, assigned by the transport layer.
        /// Defaults to <see cref="ApiKeyValidationResult.NotChecked"/>, which is correct for
        /// in-process calls: they carry no <c>X-Api-Key</c> header.
        /// </summary>
        public ApiKeyValidationResult ApiKeyValidation { get; set; } = ApiKeyValidationResult.NotChecked;

        /// <summary>
        /// Asynchronously executes an API method.
        /// </summary>
        /// <param name="request">The JSON-RPC request model.</param>
        /// <param name="cancellationToken">
        /// A token that cancels the call, such as the HTTP request's abort token or the in-process
        /// caller's own token.
        /// </param>
        /// <returns>The response; a failure of the method itself is reported in its error member.</returns>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="cancellationToken"/> was cancelled before the business object method was
        /// invoked. Cancellation is not turned into an error response: the caller asked to stop and
        /// is not waiting for one.
        /// </exception>
        /// <remarks>
        /// Business object methods are synchronous in 1.0 (ADR-046), so the token is observed up to
        /// the point of dispatch — before the call starts, and while the replay store decides — and
        /// not inside the method once it runs.
        /// </remarks>
        public async Task<JsonRpcResponse> ExecuteAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            var response = new JsonRpcResponse(request);
            var stopwatch = AnomalyEnabled ? Stopwatch.StartNew() : null;
            try
            {
                var format = request.Params.Format;

                // Parse method, create BO, and validate access BEFORE decryption.
                // This ensures unauthenticated or unauthorized requests are rejected without
                // performing any decryption work.
                var (progId, action) = ParseMethod(request.Method);
                var businessObject = CreateBusinessObject(AccessToken, progId);
                // Hand the caller's application identity to business objects that ask for it. A
                // setter rather than a constructor argument: only a few methods care (the
                // connectivity probe reports it, the audit trail records it), and widening every
                // business-object constructor would break each application subclass for their sake.
                if (businessObject is IApiKeyContextAware apiKeyAware)
                {
                    apiKeyAware.ApiKeyValidation = ApiKeyValidation;
                }
                var method = GetMethod(businessObject, action);
                ApiAccessValidator.ValidateAccess(method, new ApiCallContext(AccessToken, IsLocalCall, format), _tokenValidator);

                // Access confirmed: retrieve the encryption key and decrypt the payload.
                byte[]? apiEncryptionKey = GetApiEncryptionKey(format);
                // The frame rides inside the envelope, so the replay gate can only run once the
                // payload is decrypted — it is a second gate after ValidateAccess, not part of it.
                ApiPayloadConverter.RestoreRequest(request.Params, format, apiEncryptionKey, ActionPayloadType.Resolve(method));
                ValidateFrameTimestamp(request.Params.Frame);
                await ValidateFrameSequenceAsync(method, request.Params.Frame, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                // Invoke the method and convert the result.
                var value = await InvokeMethodAsync(businessObject, method, request.Params.Value)
                    .ConfigureAwait(false);
                value = ApiOutputConverter.Convert(value!);

                // The answer is written with the codec the caller asked for, the same way it keeps
                // the caller's format. A client that negotiated one cannot decode anything else.
                response.Result = new JsonRpcResult { Value = value, Codec = request.Params.Codec };
                ApiPayloadConverter.TransformTo(response.Result, format, apiEncryptionKey);
                LogApiSlowAnomaly(request.Method, stopwatch);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var rootEx = ex.Unwrap();
                // Map the exception to a (code, message) pair. The framework's user-facing
                // exceptions surface their original message; everything else is flattened to a
                // generic message to avoid leaking internals, and the real one is logged here.
                var (code, message) = MapException(rootEx);
                response.Error = new JsonRpcError((int)code, LocalizeMessage(rootEx, message));
                LogMaskedFailure(request.Method, rootEx, code);
                LogApiFailureAnomaly(request.Method, rootEx, stopwatch);
            }
            return response;
        }

        /// <summary>
        /// Refuses a call whose frame timestamp is too far from server time.
        /// </summary>
        /// <param name="frame">The frame read from the request, or null when none was required.</param>
        /// <exception cref="ReplayRejectedException">Thrown when the drift exceeds the configured tolerance.</exception>
        private static void ValidateFrameTimestamp(ApiPayloadFrame? frame)
        {
            if (frame == null) { return; }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long driftMs = Math.Abs(nowMs - frame.TimestampMs);
            double toleranceMs = ApiServiceOptions.WireFrameTimestampTolerance.TotalMilliseconds;

            if (driftMs > toleranceMs)
            {
                throw new ReplayRejectedException(
                    $"The request timestamp is {driftMs / 1000} seconds away from server time, outside the accepted window. Check the client clock.");
            }
        }

        /// <summary>
        /// Refuses a call whose sequence number this session has already used.
        /// </summary>
        /// <param name="method">The method being invoked, whose declaration says whether to check.</param>
        /// <param name="frame">The frame read from the request, or null when none was required.</param>
        /// <param name="cancellationToken">A token that cancels the store's decision.</param>
        /// <exception cref="ReplayRejectedException">Thrown when the sequence repeats or is out of range.</exception>
        /// <remarks>
        /// Skipped for anonymous callers: sequence numbers are counted per session, and a call made
        /// without one has nothing to count against — every anonymous caller would otherwise share
        /// a single window and evict each other's numbers.
        /// </remarks>
        private async ValueTask ValidateFrameSequenceAsync(MethodInfo method, ApiPayloadFrame? frame, CancellationToken cancellationToken)
        {
            if (frame == null || AccessToken == Guid.Empty) { return; }

            var attr = ApiAccessValidator.FindAccessControl(method);
            if (attr?.ReplayProtection != ApiReplayProtection.UniqueSequence) { return; }

            bool accepted = await ApiServiceOptions.ReplayWindowStore
                .TryAcceptAsync(AccessToken, frame.Sequence, cancellationToken).ConfigureAwait(false);
            if (!accepted)
            {
                throw new ReplayRejectedException(
                    "This request repeats a sequence number the session has already used, or falls outside the accepted range.");
            }
        }

        /// <summary>
        /// Gets the API encryption key.
        /// </summary>
        /// <param name="format">The payload encoding format for transmission.</param>
        private byte[]? GetApiEncryptionKey(PayloadFormat format)
        {
            return format == PayloadFormat.Encrypted
                ? _keyProvider.GetKey(AccessToken)
                : null;
        }

        /// <summary>
        /// The longest progId or action accepted from the wire.
        /// </summary>
        /// <remarks>
        /// Well above any name the framework or its applications use, and short enough that a name
        /// cannot become a lever: it is parsed, and handed to the business-object factory, before the
        /// caller's access has been checked.
        /// </remarks>
        internal const int MaxMethodPartLength = 64;

        /// <summary>
        /// Parses the progId and action from the Method property.
        /// </summary>
        /// <returns>A tuple containing the progId and action. Throws if the format is invalid.</returns>
        /// <remarks>
        /// The shape is checked here, before anything else sees the value: the progId may hold letters,
        /// digits, <c>_</c> and <c>-</c>, the action letters, digits and <c>_</c>, each at most
        /// <see cref="MaxMethodPartLength"/> characters. An empty progId is left to the factory's own
        /// check. The message does not echo the value, which can be as long as the request body allows.
        /// </remarks>
        private static (string progId, string action) ParseMethod(string method)
        {
            if (!string.IsNullOrEmpty(method))
            {
                var parts = method.Split(s_methodSeparators, 2);
                if (parts.Length == 2 && IsValidMethodPart(parts[0], allowHyphen: true)
                    && parts[1].Length > 0 && IsValidMethodPart(parts[1], allowHyphen: false))
                {
                    return (parts[0], parts[1]);
                }
            }
            throw new FormatException("Invalid method format.");
        }

        /// <summary>
        /// Checks the length and characters of one half of a <c>progId.action</c> method name.
        /// </summary>
        private static bool IsValidMethodPart(string part, bool allowHyphen)
        {
            if (part.Length > MaxMethodPartLength) { return false; }
            foreach (char c in part)
            {
                if (!(char.IsAsciiLetterOrDigit(c) || c == '_' || (allowHyphen && c == '-')))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Resolves the <see cref="MethodInfo"/> for the specified action on the given business object.
        /// </summary>
        /// <param name="businessObject">The business object instance.</param>
        /// <param name="action">The action name.</param>
        /// <remarks>
        /// Only public instance methods are looked up, and a match that
        /// <see cref="IsResolvableAction"/> refuses is reported exactly like a name that matched
        /// nothing, so a caller cannot tell a property accessor from an absent method.
        /// </remarks>
        private static MethodInfo GetMethod(object businessObject, string action)
        {
            var type = businessObject.GetType();
            var method = type.GetMethod(action, BindingFlags.Public | BindingFlags.Instance);
            if (method == null || !IsResolvableAction(method))
                throw new MissingMethodException($"Method '{action}' not found in business object '{type.Name}'.");
            return method;
        }

        /// <summary>
        /// Says whether a method is one a JSON-RPC action name may resolve to.
        /// </summary>
        /// <param name="method">The candidate method.</param>
        /// <returns>
        /// <c>true</c> for a public, non-generic instance method that takes exactly one parameter,
        /// is not a property or event accessor, and is not declared by <see cref="object"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// IMPORTANT: this is the rule that keeps a type-level <see cref="Polhem.Definition.Attributes.ApiAccessControlAttribute"/> from
        /// publishing more than the type's own actions. That attribute covers every method of its
        /// type, and the lookup used to accept static methods and accessors as well, so a public
        /// setter such as <c>set_X</c> became a remotely callable action at the type's protection
        /// level. Analyzer rule <c>POLHEM3001</c> applies the same rule, so what it reports and what
        /// can be called agree.
        /// </para>
        /// <para>
        /// "Not declared by <see cref="object"/>" is how "declared on a business object" is
        /// expressed at this layer, which cannot see <c>BusinessObject</c>: the framework's type
        /// resolver, <c>ProgramSettingsBoTypeResolver</c>, refuses to bind a progId to anything but a
        /// <c>BusinessObject</c> subclass, so a public instance method is either declared in that
        /// hierarchy or inherited from <see cref="object"/>. An override of an <see cref="object"/>
        /// member, such as <see cref="object.Equals(object)"/>, is refused too.
        /// </para>
        /// <para>
        /// A single parameter is required because the executor always passes exactly one argument;
        /// any other signature could only fail at invocation.
        /// </para>
        /// </remarks>
        public static bool IsResolvableAction(MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(method);
            return method.IsPublic
                && !method.IsStatic
                && !method.IsSpecialName
                && !method.IsGenericMethod
                && method.GetParameters().Length == 1
                && method.GetBaseDefinition().DeclaringType != typeof(object);
        }

        /// <summary>
        /// Converts the input argument and asynchronously invokes the specified method on the business object.
        /// </summary>
        /// <param name="businessObject">The business object instance.</param>
        /// <param name="method">The resolved method to invoke.</param>
        /// <param name="value">The deserialized input argument.</param>
        private static async Task<object?> InvokeMethodAsync(object businessObject, MethodInfo method, object? value)
        {
            // Convert the input parameter to the expected BO type if needed
            var methodParams = method.GetParameters();
            if (methodParams.Length > 0 && value != null)
            {
                var paramType = methodParams[0].ParameterType;
                value = ApiInputConverter.Convert(value, paramType);
            }

            var result = method.Invoke(businessObject, new object?[] { value });

            // If the method is asynchronous (Task or Task<T>), await it
            if (result is Task task)
            {
                // Await the asynchronous task to completion (ConfigureAwait(false) recommended in server-side environments to avoid deadlocks)
                await task.ConfigureAwait(false);
                // If it is Task<T>, extract the Result; otherwise it is Task (void) and returns null
                var taskType = task.GetType();
                var isGeneric = taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>);
                return isGeneric
                    ? taskType.GetProperty("Result")?.GetValue(task)
                    : null;
            }

            return result;
        }

        /// <summary>
        /// Maps an exception to the corresponding JSON-RPC error code and message used in
        /// the response envelope. The framework's user-facing exceptions surface their original
        /// message; every other exception returns a generic message to avoid leaking internals.
        /// </summary>
        /// <param name="ex">The exception (already unwrapped) to map.</param>
        /// <returns>A tuple of the JSON-RPC error code and the message to expose.</returns>
        /// <remarks>
        /// <para>
        /// Which exception travels as which code, and whether its own message travels with it, is
        /// declared once, in <see cref="JsonRpcErrorContract"/>, and the client rebuilds from that
        /// same declaration. What stays here is only what the contract deliberately leaves out: the
        /// fallback for an exception it does not cover, and the debug-mode exception below.
        /// </para>
        /// <para>
        /// Exposed as <c>internal</c> for direct unit testing through
        /// <c>InternalsVisibleTo</c>; the mapping is a protocol-level contract, not an
        /// implementation detail.
        /// </para>
        /// <para>
        /// In debug mode the real message is passed through instead of being replaced, both for an
        /// uncovered exception and for a BCL exception the contract gives a fixed message. The
        /// generic message is the right answer in production — such a failure should not describe
        /// the server's internals to a caller — but it leaves a developer with nothing to work
        /// from. The same trade-off is already made at the transport layer, where
        /// <c>ApiServiceController</c> attaches the real message only when the host is running in
        /// development.
        /// </para>
        /// <para>
        /// WARNING: the debug branch must stay gated on <see cref="SysInfo.IsDebugMode"/>, and
        /// must pass the message alone. A stack trace or any wider dump would leak internal paths
        /// and system detail into an API response, which <c>rules/scanning.md</c> prohibits
        /// outright.
        /// </para>
        /// </remarks>
        internal static (JsonRpcErrorCode code, string message) MapException(Exception ex)
        {
            if (JsonRpcErrorContract.TryGetCode(ex, out var code, out var fixedMessage))
                return (code, fixedMessage == null || SysInfo.IsDebugMode ? ex.Message : fixedMessage);
            return (JsonRpcErrorCode.InternalError,
                SysInfo.IsDebugMode ? ex.Message : "Internal server error");
        }

        /// <summary>
        /// Logs a failure whose own message the contract does not let through to the caller.
        /// </summary>
        /// <param name="method">The JSON-RPC method that failed.</param>
        /// <param name="rootEx">The unwrapped exception.</param>
        /// <param name="code">The code the caller received.</param>
        /// <remarks>
        /// Logged whether or not debug mode passed the message through: the log is where an
        /// operator looks, and it should not depend on how the caller was answered.
        /// </remarks>
        private void LogMaskedFailure(string method, Exception rootEx, JsonRpcErrorCode code)
        {
            if (Logger == null) { return; }
            if (JsonRpcErrorContract.TryGetCode(rootEx, out _, out var fixedMessage) && fixedMessage == null) { return; }

            if (code == JsonRpcErrorCode.InternalError)
                Logger.LogError(rootEx, "JSON-RPC method {Method} failed; the caller received a generic error message.", method);
            else
                Logger.LogWarning(rootEx, "JSON-RPC method {Method} was refused; the caller received a generic message for error code {Code}.", method, (int)code);
        }

        /// <summary>
        /// Creates an instance of the business object for the specified progId.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program identifier.</param>
        /// <returns>The business object instance.</returns>
        /// <remarks>
        /// No dispatch of its own: the reserved progIds resolve through the same registry as every
        /// other, so the transport layer has nothing left to decide. It used to branch on
        /// <c>System</c> and <c>AuditLog</c>, which meant two identifiers were progIds on the wire
        /// but not in the registry.
        /// </remarks>
        private object CreateBusinessObject(Guid accessToken, string progId)
        {
            if (string.IsNullOrWhiteSpace(progId))
                throw new ArgumentException("ProgId cannot be null or empty.", nameof(progId));

            return _boFactory.CreateBusinessObject(accessToken, progId, IsLocalCall);
        }
    }

}
