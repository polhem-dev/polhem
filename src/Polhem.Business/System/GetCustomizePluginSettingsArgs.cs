using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Arguments for reading one tenant's business plugin bindings.
    /// </summary>
    public class GetCustomizePluginSettingsArgs : BusinessArgs, IGetCustomizePluginSettingsRequest
    {
        /// <summary>
        /// Gets or sets the tenant customization code whose bindings are requested.
        /// </summary>
        public string CustomizeId { get; set; } = string.Empty;
    }
}
