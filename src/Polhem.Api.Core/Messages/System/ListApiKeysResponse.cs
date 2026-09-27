using Polhem.Api.Contracts.System;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response for the list API keys operation.
    /// </summary>
    public sealed class ListApiKeysResponse : ApiResponse, IListApiKeysResponse
    {
        /// <summary>
        /// Gets or sets the issued keys, without any credential material.
        /// </summary>
        public List<ApiKeySummary> ApiKeys { get; set; } = [];

        /// <inheritdoc/>
        IReadOnlyList<ApiKeySummary> IListApiKeysResponse.ApiKeys => ApiKeys;
    }
}
