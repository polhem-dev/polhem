using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for reading one tenant's business plugin bindings.
    /// </summary>
    public class GetCustomizePluginSettingsRequest : ApiRequest, IGetCustomizePluginSettingsRequest
    {
        /// <summary>
        /// Gets or sets the tenant customization code whose bindings are requested.
        /// </summary>
        public string CustomizeId { get; set; } = string.Empty;
    }
}
