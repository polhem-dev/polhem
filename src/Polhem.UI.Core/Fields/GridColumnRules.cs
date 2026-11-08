using Polhem.Definition.Layouts;

namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// The column sizing convention every UI head applies to a detail or list grid: a column is never narrower
    /// than its minimum width, so its header and an editor opened in its cell stay whole. When the grid is too
    /// narrow for every column (a phone-sized form), it scrolls horizontally instead of squeezing the columns.
    /// </summary>
    public static class GridColumnRules
    {
        /// <summary>
        /// The minimum width, in device-independent units (CSS pixels on the web), of a column whose layout gives
        /// no <see cref="LayoutColumn.Width"/>.
        /// </summary>
        public const int DefaultMinWidth = 120;

        /// <summary>
        /// Gets the minimum width of a column: its layout <see cref="LayoutColumn.Width"/> when one is set,
        /// otherwise <see cref="DefaultMinWidth"/>.
        /// </summary>
        /// <param name="column">The layout column.</param>
        /// <returns>The minimum width, in device-independent units.</returns>
        public static int GetMinWidth(LayoutColumn column)
        {
            ArgumentNullException.ThrowIfNull(column);
            return column.Width > 0 ? column.Width : DefaultMinWidth;
        }
    }
}
