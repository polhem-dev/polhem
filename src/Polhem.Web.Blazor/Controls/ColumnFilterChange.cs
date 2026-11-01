using Polhem.Definition.Forms;
using Polhem.UI.Core.Connectors;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// What a user entered in a grid column's filter.
    /// </summary>
    /// <param name="Field">The schema field of the column.</param>
    /// <param name="Kind">The kind of filter.</param>
    /// <param name="First">The value, or the start of a range.</param>
    /// <param name="Second">The end of a range.</param>
    public sealed record ColumnFilterChange(FormField Field, ColumnFilterKind Kind, string First, string Second);
}
