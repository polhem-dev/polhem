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
            IJSObjectReference? module = null;
            try
            {
                module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
                await module.InvokeVoidAsync("forwardEscape").ConfigureAwait(true);
                _module = module;
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
            {
                // Without the forwarder a dialog still closes on Escape pressed inside it, as MudBlazor does by itself.
                // A module that did load is kept, so that disposing the component releases it.
                _module = module;
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
