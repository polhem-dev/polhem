using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the EnterCompany operation.
    /// </summary>
    public sealed class EnterCompanyRequest : ApiRequest, IEnterCompanyRequest
    {
        /// <summary>
        /// Gets or sets the id of the company the caller wants to enter for this session.
        /// </summary>
        public string CompanyId { get; set; } = string.Empty;
    }
}
