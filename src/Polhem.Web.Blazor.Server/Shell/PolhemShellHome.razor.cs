using Microsoft.AspNetCore.Components;
using Polhem.Api.Client;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// Opens the first form of the menu, as the Avalonia shell does after sign-in. Place it on the application's root
    /// page.
    /// </summary>
    /// <remarks>
    /// <see cref="PolhemShellLayout"/> renders the routed page only once a company is entered, so the menu it reads is
    /// the signed-in user's. A menu with no form, or one that cannot be loaded, leaves the page showing nothing more;
    /// the menu beside it shows why.
    /// </remarks>
    public sealed partial class PolhemShellHome : ComponentBase
    {
        [Inject]
        private PolhemApiClient Client { get; set; } = default!;

        [Inject]
        private PolhemBlazorOptions BlazorOptions { get; set; } = default!;

        [Inject]
        private PolhemShellOptions Options { get; set; } = default!;

        [Inject]
        private NavigationManager Navigation { get; set; } = default!;

        [Inject]
        private IServiceProvider? Services { get; set; }

        private string LoadingText => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), PolhemUIText.Loading);

        /// <inheritdoc/>
        protected override async Task OnInitializedAsync()
        {
            var (rows, _) = await ShellMenuRows.TryLoadAsync(Client, BlazorOptions).ConfigureAwait(true);
            if (rows?.FirstOrDefault(r => r.ProgId is not null) is { ProgId: { } progId })
                Navigation.NavigateTo(Options.GetFormUri(progId), replace: true);
        }
    }
}
