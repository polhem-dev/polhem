using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.UI.Core
{
    /// <summary>
    /// The signed-in user's context as a front end sees it.
    /// </summary>
    /// <remarks>
    /// A front end with no backend has no context to supply; the views then treat the user as anonymous and
    /// unrestricted. The capabilities degrade the UI only; the backend remains the security boundary.
    /// </remarks>
    public interface IUserContext
    {
        /// <summary>
        /// Gets the signed-in user, or <c>null</c> when nobody is signed in.
        /// </summary>
        UserInfo? User { get; }

        /// <summary>
        /// Gets the user's capabilities per permission model, or <c>null</c> when enforcement is inactive.
        /// </summary>
        IReadOnlyDictionary<string, PermissionActions>? Capabilities { get; }

        /// <summary>
        /// Gets the company the user entered, or <c>null</c> when none is entered.
        /// </summary>
        CompanyInfo? Company { get; }

        /// <summary>
        /// Gets the deployment's default language, the last step of the UI text fall-back chain; empty when unknown.
        /// </summary>
        string DefaultLanguage { get; }
    }
}
