using Microsoft.AspNetCore.Components;
using Polhem.Api.Client;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Shell
{
    /// <summary>
    /// The state of the shell's menu, which a UI package draws: the server's
    /// <see cref="Polhem.Definition.Settings.MenuSettings"/> as folder headers and links to
    /// <see cref="PolhemShellOptions.FormRoute"/>, captions in the current UI culture.
    /// </summary>
    /// <remarks>
    /// It loads the menu once, when it is first rendered, over the circuit's <see cref="PolhemApiClient"/>; the
    /// server filters it by the signed-in user. A menu that cannot be loaded shows the error in its place.
    /// </remarks>
    public abstract class ShellNavMenuBase : ComponentBase
    {
        /// <summary>Gets or sets the callback raised when the user picks a form, so a narrow screen can close the menu.</summary>
        [Parameter]
        public EventCallback OnNavigated { get; set; }

        /// <summary>Gets the shell's options.</summary>
        [Inject]
        protected PolhemShellOptions Options { get; private set; } = default!;

        [Inject]
        private PolhemApiClient Client { get; set; } = default!;

        [Inject]
        private IBlazorDefinitionOptions BlazorOptions { get; set; } = default!;

        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets the menu's rows, or <see langword="null"/> until they load or when they could not.</summary>
        protected IReadOnlyList<ShellMenuRow>? Rows { get; private set; }

        /// <summary>Gets the message of a failed load.</summary>
        protected string? Error { get; private set; }

        /// <summary>Gets the text shown while the menu loads.</summary>
        protected string LoadingText => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), PolhemUIText.Loading);

        /// <inheritdoc/>
        protected override async Task OnInitializedAsync()
        {
            var (rows, error) = await ShellMenuRows.TryLoadAsync(Client, BlazorOptions).ConfigureAwait(true);
            Rows = rows;
            Error = error is null ? null : PolhemShellText.Format(PolhemShellText.MenuLoadFailed, error);
        }
    }
}
