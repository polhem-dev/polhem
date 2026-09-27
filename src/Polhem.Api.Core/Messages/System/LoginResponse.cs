using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response for the login operation.
    /// </summary>
    public sealed class LoginResponse : ApiResponse, ILoginResponse
    {
        /// <summary>
        /// Gets or sets the access token used for authenticating subsequent API calls.
        /// </summary>
        public Guid AccessToken { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the expiration time of the AccessToken in UTC.
        /// </summary>
        public DateTime ExpiredAt { get; set; }

        /// <summary>
        /// Gets or sets the RSA-encrypted session encryption key.
        /// </summary>
        public string ApiEncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user account identifier.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user display name.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's IANA time zone id (e.g. Asia/Taipei).
        /// </summary>
        /// <remarks>
        /// Returned so the client can render dates and seed new rows on the user's own day rather
        /// than the device's — a user filing a Taipei leave request from New York must still default
        /// to the Taipei date (ADR-032 D12).
        /// </remarks>
        public string TimeZone { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's culture (e.g. zh-TW, en-US): <c>st_user.culture</c>, or the
        /// deployment's default language when the user has none.
        /// </summary>
        /// <remarks>
        /// Returned so the client renders in the language the account says rather than the
        /// device's; the server resolves its own messages for this session in the same culture.
        /// </remarks>
        public string Culture { get; set; } = string.Empty;
    }
}
