using System.ComponentModel;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Polhem.Web.Blazor.Fluent.Controls
{
    /// <summary>
    /// A <see cref="FluentDataGrid{TGridItem}"/> that no longer adds anything to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It caught the <see cref="ObjectDisposedException"/> that Fluent UI 5.0.1 threw when a grid was removed while it was
    /// still setting itself up (https://github.com/microsoft/fluentui-blazor/issues/5580). Fluent UI 5.1.0 fixes that, and
    /// this package's grid uses <see cref="FluentDataGrid{TGridItem}"/> again.
    /// </para>
    /// <para>
    /// It belonged to this package's grid and was not meant for applications. It stays public only because it shipped.
    /// </para>
    /// </remarks>
    /// <typeparam name="TGridItem">The type of a row.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use FluentDataGrid<TGridItem>. Fluent UI 5.1.0 fixes what PolhemDataGrid worked around. PolhemDataGrid is planned for removal in 2.0.")]
    public sealed class PolhemDataGrid<TGridItem> : FluentDataGrid<TGridItem>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PolhemDataGrid{TGridItem}"/>.
        /// </summary>
        /// <param name="configuration">The library's configuration.</param>
        public PolhemDataGrid(LibraryConfiguration configuration) : base(configuration)
        {
        }
    }
}
