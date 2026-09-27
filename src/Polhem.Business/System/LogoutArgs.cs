using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Input arguments for the Logout operation. Carries no fields.
    /// </summary>
    public sealed class LogoutArgs : BusinessArgs, ILogoutRequest
    {
    }
}
