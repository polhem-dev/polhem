using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the Logout operation. Carries no payload fields.
    /// </summary>
    public class LogoutRequest : ApiRequest, ILogoutRequest
    {
    }
}
