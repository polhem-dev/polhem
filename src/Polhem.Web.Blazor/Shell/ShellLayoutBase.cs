using Microsoft.AspNetCore.Components;
using Polhem.Web.Blazor.Identity;

namespace Polhem.Web.Blazor.Shell
{
    /// <summary>
    /// The state of the shell's layout, which a UI package draws: the sign-in screen until the user has entered a
    /// company, then a top bar, the server's menu and the routed page.
    /// </summary>
    /// <remarks>
    /// The menu button collapses the menu on a wide screen and slides it over the page on a narrow one; picking a form
    /// closes the narrow overlay.
    /// </remarks>
    public abstract class ShellLayoutBase : LayoutComponentBase
    {
        /// <summary>Gets the user context the layout follows.</summary>
        [Inject]
        protected BlazorUserContext UserContext { get; private set; } = default!;

        /// <summary>Gets the shell's options.</summary>
        [Inject]
        protected PolhemShellOptions Options { get; private set; } = default!;

        [Inject]
        private NavigationManager Navigation { get; set; } = default!;

        /// <summary>
        /// Gets whether the menu is toggled. One switch for both widths: a wide screen shows the menu until it is
        /// toggled, a narrow one hides it until it is toggled. Picking a form clears it, which closes the narrow overlay
        /// and changes nothing on a wide screen, where a collapsed menu has nothing to pick.
        /// </summary>
        protected bool IsMenuToggled { get; private set; }

        /// <summary>Toggles the menu.</summary>
        protected void ToggleMenu() => IsMenuToggled = !IsMenuToggled;

        /// <summary>Clears the toggle.</summary>
        protected void CloseMenu() => IsMenuToggled = false;

        /// <summary>Signs out and returns to the sign-in screen.</summary>
        /// <remarks>The client and the context are the circuit's, so signing out is local to this browser tab.</remarks>
        protected void SignOut()
        {
            UserContext.SignOut();
            IsMenuToggled = false;
            Navigation.NavigateTo("");
        }
    }
}
