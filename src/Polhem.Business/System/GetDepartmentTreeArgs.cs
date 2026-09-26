using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Input arguments for retrieving the current company's department tree. Carries no fields —
    /// the company is resolved from the session.
    /// </summary>
    public class GetDepartmentTreeArgs : BusinessArgs, IGetDepartmentTreeRequest
    {
    }
}
