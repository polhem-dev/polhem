using Microsoft.AspNetCore.Components;
using Polhem.Web.Blazor.Server.Identity;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// A ready-made layout for a Polhem Blazor Server site: the sign-in screen until the circuit's user has entered a
    /// company, then a top bar, the server's menu and the routed page.
    /// </summary>
    /// <remarks>
    /// Set it as the router's default layout. It reads the <see cref="PolhemShellOptions"/> of
    /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/> and the circuit's
    /// <see cref="CircuitUserContext"/>. The sign-in belongs to the circuit, so a reload or a new tab shows the
    /// sign-in screen again. Signing out returns to it as well.
    /// <para>
    /// The menu button collapses the menu on a wide screen and slides it over the page below 600 CSS pixels, the
    /// default compact threshold of the form page; picking a form closes the narrow overlay. The look comes from the
    /// package's stylesheet; see the package README.
    /// </para>
    /// </remarks>
    public sealed partial class PolhemShellLayout : LayoutComponentBase
    {
        // One switch for both widths, read by the stylesheet: a wide screen shows the menu until it is toggled off, a
        // narrow one hides it until it is toggled on. Picking a form clears it, which closes the narrow overlay and
        // changes nothing on a wide screen, where a collapsed menu has nothing to pick.
        private bool _isMenuToggled;

        [Inject]
        private CircuitUserContext UserContext { get; set; } = default!;

        [Inject]
        private NavigationManager Navigation { get; set; } = default!;

        [Inject]
        private PolhemShellOptions Options { get; set; } = default!;

        private void ToggleMenu() => _isMenuToggled = !_isMenuToggled;

        private void CloseMenu() => _isMenuToggled = false;

        // The client and the context are the circuit's, so signing out is local to this browser tab.
        private void SignOut()
        {
            UserContext.SignOut();
            _isMenuToggled = false;
            Navigation.NavigateTo("");
        }
    }
}
