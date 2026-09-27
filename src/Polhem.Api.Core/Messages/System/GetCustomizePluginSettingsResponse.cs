using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response carrying one tenant's business plugin bindings.
    /// </summary>
    public sealed class GetCustomizePluginSettingsResponse : ApiResponse, IGetCustomizePluginSettingsResponse
    {
        /// <summary>
        /// Gets or sets the bindings as XML, or an empty string when the tenant declares none.
        /// </summary>
        public string Xml { get; set; } = string.Empty;
    }
}
