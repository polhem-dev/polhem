using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Result of storing one tenant's business plugin bindings.
    /// </summary>
    public sealed class SaveCustomizePluginSettingsResult : BusinessResult, ISaveCustomizePluginSettingsResponse
    {
        /// <summary>
        /// Gets or sets the number of plugin bindings stored, across every program in the definition.
        /// </summary>
        public int PluginCount { get; set; }
    }
}
