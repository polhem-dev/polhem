using Polhem.Api.Contracts.System;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API request for the Logout operation. Carries no payload fields.
    /// </summary>
    public sealed class LogoutRequest : ApiRequest, ILogoutRequest
    {
    }
}
