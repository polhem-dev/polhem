using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Shell
{
    /// <summary>
    /// Draws <see cref="PolhemShellOptions.SignInLinks"/> under <see cref="PolhemShellOptions.SignInLinksTitle"/>.
    /// A UI package places it on its sign-in screen; it draws nothing when there are no links.
    /// </summary>
    /// <remarks>
    /// A link to the application's own base address is left out, so applications that share one list, such as the
    /// front ends of one site, each show only the others. The links are plain anchors: the router hands an address it
    /// has no route for to the browser, which loads that page.
    /// </remarks>
    public sealed partial class PolhemShellSignInLinks : ComponentBase
    {
        private List<PolhemShellLink> Links { get; set; } = [];

        [Inject]
        private PolhemShellOptions Options { get; set; } = default!;

        [Inject]
        private NavigationManager Navigation { get; set; } = default!;

        private string? AriaLabel
            => string.IsNullOrWhiteSpace(Options.SignInLinksTitle) ? null : Options.SignInLinksTitle;

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            var self = new Uri(Navigation.BaseUri);
            Links = [.. Options.SignInLinks.Where(link => !IsSelf(link, self))];
        }

        private static bool IsSelf(PolhemShellLink link, Uri self)
            => Uri.TryCreate(self, link.Href, out var target)
                && Uri.Compare(target, self, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0;
    }
}
