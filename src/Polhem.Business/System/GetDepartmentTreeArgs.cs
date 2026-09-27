using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Input arguments for retrieving the current company's department tree. Carries no fields —
    /// the company is resolved from the session.
    /// </summary>
    public sealed class GetDepartmentTreeArgs : BusinessArgs, IGetDepartmentTreeRequest
    {
    }
}
