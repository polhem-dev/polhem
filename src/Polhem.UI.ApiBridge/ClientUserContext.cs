using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.UI.Core;

namespace Polhem.UI.ApiBridge
{
    /// <summary>
    /// The <see cref="IUserContext"/> of a single-user head, read from <see cref="ClientInfo"/>.
    /// </summary>
    /// <remarks>
    /// Every member reads <see cref="ClientInfo"/> when it is called, so a sign-in or a company switch needs no
    /// synchronization. Not for a host that serves several users from one process; see <see cref="ClientInfo"/>.
    /// </remarks>
    public sealed class ClientUserContext : IUserContext
    {
        /// <inheritdoc/>
        public UserInfo? User => ClientInfo.UserInfo;

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, PermissionActions>? Capabilities => ClientInfo.Capabilities;

        /// <inheritdoc/>
        public CompanyInfo? Company => ClientInfo.Company;

        /// <inheritdoc/>
        public string DefaultLanguage => ClientInfo.ApiClient.DefaultLanguage;
    }
}
