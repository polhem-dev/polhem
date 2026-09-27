using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Output result for the Logout operation. Carries no fields.
    /// </summary>
    public sealed class LogoutResult : BusinessResult, ILogoutResponse
    {
    }
}
