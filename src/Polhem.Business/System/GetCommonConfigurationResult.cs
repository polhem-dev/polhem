using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Output result for retrieving common parameters and environment configuration.
    /// </summary>
    public sealed class GetCommonConfigurationResult : BusinessResult, IGetCommonConfigurationResponse
    {
        /// <summary>
        /// Gets or sets the common parameters and environment configuration.
        /// </summary>
        public string CommonConfiguration { get; set; } = string.Empty;
    }
}
