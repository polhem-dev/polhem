using Microsoft.JSInterop;

namespace Polhem.Web.Blazor.Mud.Controls
{
    /// <summary>
    /// The script module that holds keys for a grid's cells, loaded once per grid because a call per cell would be
    /// thousands of messages on a circuit.
    /// </summary>
    public sealed class GridEditorScript : IAsyncDisposable
    {
        private const string ModulePath = "./_content/Polhem.Web.Blazor.Mud/polhem-grid-editor.js";

        private readonly IJSRuntime _js;
        private ValueTask<IJSObjectReference>? _loading;
        private IJSObjectReference? _module;

        /// <summary>
        /// Initializes a new instance of <see cref="GridEditorScript"/>.
        /// </summary>
        /// <param name="js">The circuit's script runtime.</param>
        public GridEditorScript(IJSRuntime js) => _js = js;

        /// <summary>
        /// Loads the module if it is not loaded yet.
        /// </summary>
        /// <returns>The operation.</returns>
        public async Task LoadAsync()
        {
            if (_module is not null) return;
            _loading ??= _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _module = await _loading.Value.Preserve().ConfigureAwait(true);
        }

        /// <summary>
        /// Gives an editor that just rendered the focus and any held keys.
        /// </summary>
        /// <param name="editor">The element around the editor.</param>
        /// <param name="cell">The cell that script tells when the user presses Escape.</param>
        /// <returns>The operation.</returns>
        public async Task FocusEditorAsync(Microsoft.AspNetCore.Components.ElementReference editor, DotNetObjectReference<GridEditorCell> cell)
        {
            await LoadAsync().ConfigureAwait(true);
            if (_module is not null) await _module.InvokeVoidAsync("focusEditor", editor, cell).ConfigureAwait(true);
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
