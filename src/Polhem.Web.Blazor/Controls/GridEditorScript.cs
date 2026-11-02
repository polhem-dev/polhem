using Microsoft.JSInterop;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The script module that holds keys for a grid's cells, loaded once per grid because a call per cell would be
    /// thousands of messages on a circuit.
    /// </summary>
    public sealed class GridEditorScript : IAsyncDisposable
    {
        private const string ModulePath = "./_content/Polhem.Web.Blazor/polhem-grid-editor.js";

        private readonly IJSRuntime _js;
        private readonly string _openPopupSelector;
        private ValueTask<IJSObjectReference>? _loading;
        private IJSObjectReference? _module;

        /// <summary>
        /// Initializes a new instance of <see cref="GridEditorScript"/>.
        /// </summary>
        /// <param name="js">The circuit's script runtime.</param>
        /// <param name="openPopupSelector">
        /// The CSS selector that matches a popup of the UI framework while it is open. Escape then closes the popup and
        /// leaves the editor open.
        /// </param>
        public GridEditorScript(IJSRuntime js, string openPopupSelector)
        {
            _js = js;
            _openPopupSelector = openPopupSelector;
        }

        /// <summary>
        /// Loads the module if it is not loaded yet.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The operation.</returns>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (_module is not null) return;
            _loading ??= _js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            _module = await _loading.Value.Preserve().ConfigureAwait(true);
        }

        /// <summary>
        /// Gives an editor that just rendered the focus and any held keys.
        /// </summary>
        /// <param name="editor">The element around the editor.</param>
        /// <param name="cell">The cell that script tells when the user presses Escape.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        /// <returns>The operation.</returns>
        public async Task FocusEditorAsync(Microsoft.AspNetCore.Components.ElementReference editor, DotNetObjectReference<GridEditorCell> cell,
            CancellationToken cancellationToken = default)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            if (_module is not null) await _module.InvokeVoidAsync("focusEditor", cancellationToken, editor, cell, _openPopupSelector).ConfigureAwait(true);
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
