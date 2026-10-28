using Microsoft.AspNetCore.Components;
using Polhem.Api.Client;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Server.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.Shell
{
    /// <summary>
    /// The shell's menu: the server's <see cref="Polhem.Definition.Settings.MenuSettings"/> as folder headers and links
    /// to <see cref="PolhemShellOptions.FormRoute"/>, captions in the circuit's UI culture.
    /// </summary>
    /// <remarks>
    /// It loads the menu once, when it is first rendered, over the circuit's <see cref="PolhemApiClient"/>; the
    /// server filters it by the signed-in user. A menu that cannot be loaded shows the error in its place.
    /// </remarks>
    public sealed partial class PolhemShellNavMenu : ComponentBase
    {
        private IReadOnlyList<ShellMenuRow>? _rows;
        private string? _error;

        /// <summary>Gets or sets the callback raised when the user picks a form, so a narrow screen can close the menu.</summary>
        [Parameter]
        public EventCallback OnNavigated { get; set; }

        [Inject]
        private PolhemApiClient Client { get; set; } = default!;

        [Inject]
        private PolhemBlazorOptions BlazorOptions { get; set; } = default!;

        [Inject]
        private PolhemShellOptions Options { get; set; } = default!;

        [Inject]
        private IServiceProvider? Services { get; set; }

        private string LoadingText => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), PolhemUIText.Loading);

        /// <inheritdoc/>
        protected override async Task OnInitializedAsync()
        {
            var (rows, error) = await ShellMenuRows.TryLoadAsync(Client, BlazorOptions).ConfigureAwait(true);
            _rows = rows;
            _error = error is null ? null : PolhemShellText.Format(PolhemShellText.MenuLoadFailed, error);
        }
    }
}
