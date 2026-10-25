using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Reports whether the browser's viewport is narrower than <see cref="CompactWidthThreshold"/>, the
    /// counterpart of the responsive switch of the Avalonia head's <c>Polhem.UI.Avalonia.Views.FormView</c>.
    /// It renders nothing; <see cref="CompactChanged"/> is raised only when the viewport crosses the threshold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="FormPage"/> places one itself. A page that uses
    /// <see cref="Polhem.Web.Blazor.Server.Controls.FormLayoutPresenter"/> directly places one next to it and passes
    /// the reported value to <see cref="Polhem.Web.Blazor.Server.Controls.FormLayoutPresenter.IsCompact"/>.
    /// </para>
    /// <para>
    /// The width is measured in the browser by a JavaScript module that ships in this package as a static web
    /// asset, loaded through JavaScript isolation; the host page adds no script, but must serve static web assets,
    /// as the Blazor templates do. The module loads after the first interactive render, so a
    /// prerendered page starts wide and switches once the circuit connects. The viewport is measured, not the
    /// component's container.
    /// </para>
    /// </remarks>
    public sealed partial class CompactViewportObserver : ComponentBase, IAsyncDisposable
    {
        /// <summary>
        /// The default <see cref="CompactWidthThreshold"/>, in CSS pixels: the same value as the Avalonia head's
        /// default.
        /// </summary>
        public const double DefaultCompactWidthThreshold = 600;

        internal const string ModulePath = "./_content/Polhem.Web.Blazor.Server/Components/CompactViewportObserver.razor.js";

        // The module is shared by every observer of the browser tab, so each one keys its listener by this id.
        private readonly string _watchId = Guid.NewGuid().ToString("N");
        private IJSObjectReference? _module;
        private DotNetObjectReference<Callback>? _callback;
        private double _watchedThreshold;
        private bool _isCompact;
        private bool _isDisposed;

        [Inject]
        private IJSRuntime JS { get; set; } = default!;

        /// <summary>
        /// Gets or sets the viewport width, in CSS pixels, below which the viewport counts as compact. Defaults to
        /// <see cref="DefaultCompactWidthThreshold"/>.
        /// </summary>
        [Parameter]
        public double CompactWidthThreshold { get; set; } = DefaultCompactWidthThreshold;

        /// <summary>
        /// Gets or sets the callback raised with <c>true</c> when the viewport becomes narrower than
        /// <see cref="CompactWidthThreshold"/> and with <c>false</c> when it becomes as wide or wider. The
        /// viewport counts as wide until the first report, so a viewport that is wide when the module loads raises
        /// nothing.
        /// </summary>
        [Parameter]
        public EventCallback<bool> CompactChanged { get; set; }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            // JavaScript cannot be called while prerendering, and the first interactive render is the earliest
            // point after it.
            if (firstRender)
            {
                await StartAsync().ConfigureAwait(true);
            }
            else if (_module is not null && !_watchedThreshold.Equals(CompactWidthThreshold))
            {
                _watchedThreshold = CompactWidthThreshold;
                var compact = await _module.InvokeAsync<bool>("update", _watchId, _watchedThreshold).ConfigureAwait(true);
                await ReportAsync(compact).ConfigureAwait(true);
            }
        }

        private async Task StartAsync()
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            if (_isDisposed)
            {
                // Disposed while the module loaded: nothing is listening yet, so only the module is released.
                await ReleaseAsync(module, isWatching: false).ConfigureAwait(true);
                return;
            }

            _module = module;
            _callback = DotNetObjectReference.Create(new Callback(this));
            _watchedThreshold = CompactWidthThreshold;
            var compact = await module.InvokeAsync<bool>("start", _callback, _watchId, _watchedThreshold).ConfigureAwait(true);
            await ReportAsync(compact).ConfigureAwait(true);
        }

        private async Task ReportAsync(bool compact)
        {
            if (_isDisposed || compact == _isCompact) return;
            _isCompact = compact;
            await CompactChanged.InvokeAsync(compact).ConfigureAwait(true);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _isDisposed = true;
            try
            {
                if (_module is { } module)
                    await ReleaseAsync(module, isWatching: true).ConfigureAwait(true);
            }
            finally
            {
                _callback?.Dispose();
            }
        }

        private async Task ReleaseAsync(IJSObjectReference module, bool isWatching)
        {
            try
            {
                if (isWatching)
                    await module.InvokeVoidAsync("stop", _watchId).ConfigureAwait(true);
                await module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone, and the browser's listener and module went with it, so nothing is left to release.
            }
        }

        // The target of the module's callback. It keeps the JSInvokable method off the component's public surface.
        private sealed class Callback
        {
            private readonly CompactViewportObserver _owner;

            public Callback(CompactViewportObserver owner) => _owner = owner;

            [JSInvokable]
            public Task OnCompactChanged(bool compact) => _owner.InvokeAsync(() => _owner.ReportAsync(compact));
        }
    }
}
