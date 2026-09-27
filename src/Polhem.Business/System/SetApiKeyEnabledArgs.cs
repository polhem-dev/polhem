using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Input arguments for enabling or disabling an issued API key.
    /// </summary>
    public sealed class SetApiKeyEnabledArgs : BusinessArgs, ISetApiKeyEnabledRequest
    {
        /// <summary>
        /// Gets or sets the key identifier being enabled or disabled.
        /// </summary>
        public string SysId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the key is accepted from now on.
        /// </summary>
        public bool Enabled { get; set; }
    }
}
