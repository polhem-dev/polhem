using System.ComponentModel;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Polhem.Web.Blazor.Fluent.Controls
{
    /// <summary>
    /// A <see cref="FluentDataGrid{TGridItem}"/> that survives being removed while it is still setting itself up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: with <c>ResizableColumns</c> or <c>ReorderableColumns</c> on, <c>OnAfterRenderAsync</c> of the grid in Fluent UI
    /// 5.0.1 awaits a script call and then calls the module again, and a grid disposed in between calls a disposed module. The
    /// exception ends the circuit unless an error boundary that stays on the page catches it, which then shows its error
    /// content for a grid that was only leaving. A list that becomes cards when the viewport is measured does this.
    /// <c>FluentCardListTests</c> fail without this type. Remove it when the upstream issue is fixed:
    /// https://github.com/microsoft/fluentui-blazor/issues/5580
    /// </para>
    /// <para>
    /// It belongs to this package's grid and is not meant for applications. It is public only because Razor places a
    /// component by its tag only when the type is public.
    /// </para>
    /// </remarks>
    /// <typeparam name="TGridItem">The type of a row.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
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
