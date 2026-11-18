using Polhem.UI.Core.Criteria;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// A condition in effect, as a chip above a list shows it: the row and the line that describes it.
    /// </summary>
    public sealed class CriteriaChip
    {
        /// <summary>Initializes a new instance of <see cref="CriteriaChip"/>.</summary>
        /// <param name="row">The condition row.</param>
        /// <param name="text">The line that describes it, from <see cref="CriteriaRowSummary.Describe"/>.</param>
        public CriteriaChip(CriteriaRow row, string text)
        {
            ArgumentNullException.ThrowIfNull(row);
            Row = row;
            Text = text ?? string.Empty;
        }

        /// <summary>Gets the condition row.</summary>
        public CriteriaRow Row { get; }

        /// <summary>Gets the line that describes the row.</summary>
        public string Text { get; }

        /// <summary>Gets a value indicating whether the chip offers to remove its condition; a required one cannot be.</summary>
        public bool IsRemovable => Row.Kind != CriteriaRowKind.Required;
    }
}
