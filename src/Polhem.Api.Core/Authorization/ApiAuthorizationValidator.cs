using Polhem.Api.Core.JsonRpc;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.Authorization
{
    /// <summary>
    /// Provides default API key and authorization validation logic.
    /// </summary>
    /// <remarks>
    /// The strength of the API key check depends on whether the deployment has issued any keys.
    /// Once <c>st_api_key</c> holds an enabled key, <see cref="IApiKeyValidator"/> has already
    /// compared the supplied key against its stored hash and this validator enforces the verdict.
    /// Until then a non-empty <c>X-Api-Key</c> passes, so a deployment works before its first key
    /// is issued, and a host that calls <c>AddPolhemApiKeyGateCheck</c> logs a startup error pointing at key
    /// management (a warning when the host runs in the Development environment).
    /// <para>
    /// Either way, user authentication is the Bearer access token's job: the API key identifies the
    /// calling application, not the user.
    /// </para>
    /// </remarks>
    public class ApiAuthorizationValidator : IApiAuthorizationValidator
    {
        /// <summary>
        /// The single message returned for every API key rejection.
        /// </summary>
        /// <remarks>
        /// WARNING: keep this identical for missing, malformed, unknown, disabled and expired keys.
        /// Distinguishing them would let a caller use the error surface to discover which keys
        /// exist; the reasons are separated in the audit record instead.
        /// </remarks>
        private const string ApiKeyRejectedMessage = "Missing or invalid API key.";

        /// <summary>
        /// The set of methods that do not require an API key (case-sensitive).
        /// </summary>
        /// <remarks>
        /// This is the application-identity axis only. Whether a method needs a signed-in caller is
        /// not decided here at all: that is <see cref="Polhem.Definition.Attributes.ApiAccessControlAttribute"/>'s job, read by the access check of the JSON-RPC pipeline (<see cref="Polhem.Api.Core.Dispatch.PolhemAccessFilter"/>).
        /// <para>
        /// <c>System.Ping</c> is exempt because a health check must still answer when the database
        /// is unavailable — the key lookup cannot be consulted then, and every other method fails
        /// closed in that state.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> s_noApiKeyMethods =
        [
            "System.Ping"
        ];

        /// <summary>
        /// Determines whether a request for the specified JSON-RPC method must carry an
        /// <c>Authorization</c> header.
        /// </summary>
        /// <param name="method">The JSON-RPC method name (case-sensitive).</param>
        /// <returns><c>true</c> if the header is mandatory; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// The default demands it for no method. A request without the header proceeds as an
        /// anonymous call, with an empty access token, and the pipeline's access check — which reads
        /// the method's <see cref="Polhem.Definition.Attributes.ApiAccessControlAttribute"/> — refuses it unless the method is declared
        /// <c>Anonymous</c>. That declaration is therefore the single source for which methods need
        /// a session; a second list here used to disagree with it, demanding a header for
        /// <c>GetCommonConfiguration</c> and <c>ExecFuncAnonymous</c> while exempting a method that no
        /// longer existed.
        /// </para>
        /// <para>
        /// The header was never authentication at this layer: it is only parsed, and any well-formed
        /// token — the empty one included — passes. When it is present it must still be a well-formed
        /// <c>Bearer</c> token. Override this to have the transport refuse a header-less request
        /// before it reaches the access check.
        /// </para>
        /// </remarks>
        protected virtual bool IsAuthorizationRequired(string method)
        {
            return false;
        }

        /// <summary>
        /// Determines whether the specified JSON-RPC method requires a valid API key.
        /// </summary>
        /// <param name="method">The JSON-RPC method name (case-sensitive).</param>
        /// <returns><c>true</c> if an API key is required; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Override to exempt a deployment's own health-check methods. Keep the list minimal: every
        /// exempt method is reachable without knowing which application is calling.
        /// </remarks>
        protected virtual bool IsApiKeyRequired(string method)
        {
            return !s_noApiKeyMethods.Contains(method);
        }

        /// <summary>
        /// Validates the API key and authorization information.
        /// </summary>
        /// <param name="context">The API authorization validation context.</param>
        /// <returns>The authorization validation result.</returns>
        public ApiAuthorizationResult Validate(ApiAuthorizationContext context)
        {
            // Validate that the input context is not null
            if (context == null)
            {
                return ApiAuthorizationResult.Fail(JsonRpcErrorCode.InvalidRequest, "Invalid authorization context.");
            }

            // Validate the API key, unless this method is exempt from application identity.
            if (IsApiKeyRequired(context.Method) && !IsApiKeyAccepted(context))
            {
                return ApiAuthorizationResult.Fail(JsonRpcErrorCode.InvalidRequest, ApiKeyRejectedMessage);
            }

            // No header: an anonymous call, unless this deployment demands the header for the method.
            // Whether the method admits anonymous callers is the access check's decision, from the
            // method's own access declaration.
            if (string.IsNullOrWhiteSpace(context.Authorization))
            {
                return IsAuthorizationRequired(context.Method)
                    ? ApiAuthorizationResult.Fail(JsonRpcErrorCode.InvalidRequest, "Missing Authorization header.")
                    : ApiAuthorizationResult.Success(Guid.Empty);
            }

            // Verify that the Authorization header uses the Bearer token format
            if (!context.Authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return ApiAuthorizationResult.Fail(JsonRpcErrorCode.InvalidRequest, "Invalid Authorization format. Expected 'Bearer <token>'.");
            }

            // Parse the Bearer token and validate it as a valid Guid
            var tokenPart = context.Authorization.Substring("Bearer ".Length).Trim();
            if (!Guid.TryParse(tokenPart, out var accessToken))
            {
                return ApiAuthorizationResult.Fail(JsonRpcErrorCode.InvalidRequest, "Invalid access token.");
            }

            // Additional validation logic can be added here, such as checking the access token against the database
            return ApiAuthorizationResult.Success(accessToken);
        }

        /// <summary>
        /// Applies the API key verdict carried on the context.
        /// </summary>
        /// <param name="context">The API authorization validation context.</param>
        /// <returns><c>true</c> when the call may proceed past the key gate.</returns>
        /// <remarks>
        /// The gate is only in force once the deployment has issued a key. Before that
        /// (<see cref="ApiKeyStatus.NotConfigured"/>), and for in-process calls that never carry a
        /// header (<see cref="ApiKeyStatus.NotChecked"/>), a presence-only check applies.
        /// </remarks>
        private static bool IsApiKeyAccepted(ApiAuthorizationContext context)
        {
            var validation = context.ApiKeyValidation ?? ApiKeyValidationResult.NotChecked;
            return validation.Status switch
            {
                ApiKeyStatus.Valid => true,
                ApiKeyStatus.NotConfigured or ApiKeyStatus.NotChecked
                    => !string.IsNullOrWhiteSpace(context.ApiKey),
                _ => false,
            };
        }
    }
}
