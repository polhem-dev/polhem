using Polhem.Definition.Security;

namespace Polhem.Api.Contracts.System
{
    /// <summary>
    /// Contract interface for the list API keys response.
    /// </summary>
    public interface IListApiKeysResponse
    {
        /// <summary>
        /// Gets the issued keys, enabled and disabled alike, without any credential material.
        /// </summary>
        IReadOnlyList<ApiKeySummary> ApiKeys { get; }
    }
}
