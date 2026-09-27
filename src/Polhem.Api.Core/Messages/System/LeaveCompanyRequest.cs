using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the LeaveCompany operation. Carries no payload fields.
    /// </summary>
    public sealed class LeaveCompanyRequest : ApiRequest, ILeaveCompanyRequest
    {
    }
}
