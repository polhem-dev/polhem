using Polhem.Api.Contracts.System;
using System.Text.Json.Serialization;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the create session operation.
    /// </summary>
    public class CreateSessionRequest : ApiRequest, ICreateSessionRequest
    {
        /// <summary>
        /// Gets or sets the user identifier.
        /// </summary>
        public string UserID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the session expiration time in seconds.
        /// </summary>
        // Written even when 0/default: the initialiser is not the CLR default, so omitting the value would let the
        // reader substitute the initialiser. Enforced for every wire member by `WireDefaultOmissionTests`.
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int ExpiresIn { get; set; } = 3600;
    }
}
