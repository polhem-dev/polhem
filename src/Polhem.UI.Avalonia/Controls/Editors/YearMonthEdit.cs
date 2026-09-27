using Polhem.Definition.Layouts;

namespace Polhem.UI.Avalonia.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.YearMonthEdit"/>: a <see cref="DateEdit"/>
    /// without the day column that binds as <c>yyyy-MM</c>.
    /// </summary>
    public sealed class YearMonthEdit : DateEdit
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YearMonthEdit"/>.
        /// </summary>
        public YearMonthEdit()
        {
            DayVisible = false;
        }

        /// <inheritdoc />
        protected override string ValueFormat => "yyyy-MM";

        /// <inheritdoc />
        /// <remarks>The culture's year-month pattern, <c>Y</c>.</remarks>
        protected override string DisplayFormat => "Y";
    }
}
