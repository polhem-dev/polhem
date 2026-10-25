using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.YearMonthEdit"/>: a <see cref="DateEdit"/> on the browser's
    /// month input, writing the value back as <c>yyyy-MM</c>.
    /// </summary>
    public sealed class YearMonthEdit : DateEdit
    {
        /// <inheritdoc />
        protected override string InputType => "month";

        /// <inheritdoc />
        protected override string ValueFormat => "yyyy-MM";

        /// <inheritdoc />
        protected override string DisplayFormat => "Y";

        /// <inheritdoc />
        protected override string ModifierClass => "polhem-field-editor--month";
    }
}
