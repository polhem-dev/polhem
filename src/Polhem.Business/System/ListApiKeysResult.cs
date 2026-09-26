using Polhem.Api.Contracts.System;
using Polhem.Definition.Security;

namespace Polhem.Business.System
{
    /// <summary>
    /// Output result for listing the issued API keys.
    /// </summary>
    public class ListApiKeysResult : BusinessResult, IListApiKeysResponse
    {
        /// <summary>
        /// Gets or sets the issued keys, without any credential material.
        /// </summary>
        public List<ApiKeySummary> ApiKeys { get; set; } = [];

        /// <inheritdoc/>
        IReadOnlyList<ApiKeySummary> IListApiKeysResponse.ApiKeys => ApiKeys;
    }
}
