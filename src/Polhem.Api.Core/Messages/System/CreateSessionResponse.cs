using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response for the create session operation.
    /// </summary>
    public sealed class CreateSessionResponse : ApiResponse, ICreateSessionResponse
    {
        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        public Guid AccessToken { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the expiration time of the AccessToken in UTC.
        /// </summary>
        public DateTime ExpiredAt { get; set; }
    }
}
