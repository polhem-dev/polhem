using Microsoft.FluentUI.AspNetCore.Components;

namespace Polhem.Web.Blazor.Fluent.Controls
{
    /// <summary>
    /// A <see cref="FluentDataGrid{TGridItem}"/> that survives being removed while it is still setting itself up.
    /// </summary>
    /// <remarks>
    /// WARNING: <c>OnAfterRenderAsync</c> of the grid in Fluent UI 5.0.1 awaits script calls, and a grid disposed in between
    /// goes on to call a disposed module. The exception ends the circuit, and an error boundary cannot catch it because the
    /// component is gone. A list that becomes cards as soon as the viewport is measured does this every time, and a slow
    /// link widens the window. Remove this type when the grid stops doing it.
    /// </remarks>
    /// <typeparam name="TGridItem">The type of a row.</typeparam>
    public sealed class PolhemDataGrid<TGridItem> : FluentDataGrid<TGridItem>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PolhemDataGrid{TGridItem}"/>.
        /// </summary>
        /// <param name="configuration">The library's configuration.</param>
        public PolhemDataGrid(LibraryConfiguration configuration) : base(configuration)
        {
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            try
            {
                await base.OnAfterRenderAsync(firstRender).ConfigureAwait(true);
            }
            catch (ObjectDisposedException)
            {
                // The grid left the page, so the script it was about to call has nothing to do.
            }
        }
    }
}
