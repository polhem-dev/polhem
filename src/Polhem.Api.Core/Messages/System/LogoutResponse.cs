using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response for the Logout operation. Carries no payload fields.
    /// </summary>
    public sealed class LogoutResponse : ApiResponse, ILogoutResponse
    {
    }
}
