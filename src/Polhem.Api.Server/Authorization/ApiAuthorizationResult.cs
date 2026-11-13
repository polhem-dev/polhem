using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Server.Authorization
{
    /// <summary>
    /// API authorization validation result.
    /// </summary>
    public sealed class ApiAuthorizationResult
    {
        /// <summary>
        /// Gets a value indicating whether validation succeeded.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets the error code.
        /// </summary>
        public JsonRpcErrorCode Code { get; init; }

        /// <summary>
        /// Gets the error message when validation fails.
        /// </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Gets the access token returned upon successful validation.
        /// </summary>
        public Guid AccessToken { get; init; } = Guid.Empty;

        /// <summary>
        /// Creates a successful authorization result.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        public static ApiAuthorizationResult Success(Guid accessToken)
        {
            return new ApiAuthorizationResult
            {
                IsValid = true,
                AccessToken = accessToken
            };
        }

        /// <summary>
        /// Creates a failed authorization result.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="errorMessage">The error message.</param>
        public static ApiAuthorizationResult Fail(JsonRpcErrorCode code,  string errorMessage)
        {
            return new ApiAuthorizationResult
            {
                IsValid = false,
                Code = code,
                ErrorMessage = errorMessage
            };
        }
    }
}
