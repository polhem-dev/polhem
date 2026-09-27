using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Arguments for storing one tenant's business plugin bindings.
    /// </summary>
    public sealed class SaveCustomizePluginSettingsArgs : BusinessArgs, ISaveCustomizePluginSettingsRequest
    {
        /// <summary>
        /// Gets or sets the tenant customization code whose bindings are being stored.
        /// </summary>
        public string CustomizeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the bindings as XML. Replaces the tenant's bindings outright.
        /// </summary>
        public string Xml { get; set; } = string.Empty;
    }
}
