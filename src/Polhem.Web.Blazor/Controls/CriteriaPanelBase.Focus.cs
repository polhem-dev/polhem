using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// Where <see cref="CriteriaPanelBase"/> puts the focus: on the first row's value when the pane opens, or on "add
    /// condition" when it has no rows, and on a row's value when the row is added. The UI package marks the pane's root
    /// with <see cref="PaneElement"/> and the rows with the shared class names; the module in this package finds them.
    /// </summary>
    public abstract partial class CriteriaPanelBase : IAsyncDisposable
    {
        private const string ModulePath = "./_content/Polhem.Web.Blazor/polhem-criteria-panel.js";

        // The row whose value takes the focus after the next render; the first row, or "add condition", when the pane opens.
        private int? _focusRow = 0;
        private IJSObjectReference? _module;
        private bool _isDisposed;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IJSRuntime? JS { get; set; }

        /// <summary>Gets or sets the pane's root element, which the UI package's markup captures.</summary>
        protected ElementReference PaneElement { get; set; }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (_focusRow is not { } index || JS is null) return;
            _focusRow = null;
            try
            {
                _module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
                if (_isDisposed || _module is null) return;
                await _module.InvokeVoidAsync("focusRow", PaneElement, index).ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone, and with it the pane the focus was meant for.
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _isDisposed = true;
            if (_module is null) return;
            try
            {
                await _module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone, and the module went with it.
            }
            GC.SuppressFinalize(this);
        }

        // A row added by the user takes the focus, so the value can be typed at once.
        private void FocusAddedRow() => _focusRow = Criteria.Rows.Count - 1;
    }
}
