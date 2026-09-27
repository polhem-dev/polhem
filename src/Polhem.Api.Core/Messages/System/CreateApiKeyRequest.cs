using Polhem.Api.Contracts.System;
using Polhem.Definition.Security;
using System.Text.Json.Serialization;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the create API key operation.
    /// </summary>
    public sealed class CreateApiKeyRequest : ApiRequest, ICreateApiKeyRequest
    {
        /// <summary>
        /// Gets or sets the key identifier to issue.
        /// </summary>
        public string SysId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the application this key is for.
        /// </summary>
        public string SysName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the key classification.
        /// </summary>
        // Written even when 0/default: the initialiser is not the CLR default, so omitting the value would let the
        // reader substitute the initialiser. Enforced for every wire member by `WireDefaultOmissionTests`.
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public ApiKeyType KeyType { get; set; } = ApiKeyType.Internal;

        /// <summary>
        /// Gets or sets the contact for the third party holding this key.
        /// </summary>
        public string? Contact { get; set; }

        /// <summary>
        /// Gets or sets the UTC expiry, or <c>null</c> for a key that does not expire.
        /// </summary>
        public DateTime? ExpiredAt { get; set; }
    }
}
