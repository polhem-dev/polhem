using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Polhem.Web.Blazor.Mud.Theming
{
    /// <summary>
    /// Loads the script that hands an Escape pressed while the focus is on the body to the topmost dialog, once per page.
    /// </summary>
    internal sealed class DialogEscapeForwarder : ComponentBase, IAsyncDisposable
    {
        private const string ModulePath = "./_content/Polhem.Web.Blazor.Mud/polhem-mud-dialog.js";

        private IJSObjectReference? _module;

        [Inject]
        private IJSRuntime JS { get; set; } = null!;

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;
            try
            {
                _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
                await _module.InvokeVoidAsync("forwardEscape").ConfigureAwait(true);
            }
            catch (JSException)
            {
                // Without the forwarder a dialog still closes on Escape pressed inside it, as MudBlazor does by itself.
                _module = null;
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_module is null) return;
            try
            {
                await _module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone, and the module with it.
            }
        }
    }
}
