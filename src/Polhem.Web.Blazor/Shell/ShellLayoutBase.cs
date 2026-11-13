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
    public abstract class ShellLayoutBase : LayoutComponentBase, IDisposable
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

        /// <summary>
        /// Gets whether the screen is narrow, as the compact viewport observer last reported. The threshold is the form
        /// page's, so the menu and the page switch together.
        /// </summary>
        protected bool IsNarrow { get; private set; }

        /// <summary>
        /// Gets whether the menu is shown: on a wide screen unless it is toggled, on a narrow one only while it is toggled.
        /// </summary>
        protected bool IsDrawerOpen => IsNarrow ? IsMenuToggled : !IsMenuToggled;

        /// <summary>Follows a change of width and clears the toggle, so the menu starts in its default state.</summary>
        /// <param name="compact">Whether the screen is now narrow.</param>
        protected void OnCompactChanged(bool compact)
        {
            IsNarrow = compact;
            CloseMenu();
        }

        /// <summary>Follows a menu that opened or closed itself, such as a drawer closed by a click outside it.</summary>
        /// <param name="open">Whether the menu is now shown.</param>
        protected void OnDrawerOpenChanged(bool open)
        {
            if (open != IsDrawerOpen) ToggleMenu();
        }

        /// <summary>Toggles the menu.</summary>
        protected void ToggleMenu() => IsMenuToggled = !IsMenuToggled;

        /// <summary>Clears the toggle.</summary>
        protected void CloseMenu() => IsMenuToggled = false;

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            base.OnInitialized();
            UserContext.SessionRejected += OnSessionRejected;
        }

        // The server ended the session, so the layout signs out and goes back to its sign-in screen. The context is
        // cleared here, on the dispatcher, because a render reads it.
        private void OnSessionRejected(object? sender, EventArgs e) => _ = InvokeAsync(() =>
        {
            UserContext.SignOut();
            IsMenuToggled = false;
            Navigation.NavigateTo("");
            StateHasChanged();
        });

        /// <inheritdoc/>
        public void Dispose()
        {
            UserContext.SessionRejected -= OnSessionRejected;
            GC.SuppressFinalize(this);
        }

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
