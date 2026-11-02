using Microsoft.FluentUI.AspNetCore.Components;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Fluent.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.YearMonthEdit"/>: a <see cref="DateEdit"/> on the month view, writing the
    /// value back as <c>yyyy-MM</c>.
    /// </summary>
    public sealed class YearMonthEdit : DateEdit
    {
        /// <inheritdoc />
        protected override string ValueFormat => "yyyy-MM";

        /// <inheritdoc />
        protected override string DisplayFormat => "Y";

        /// <inheritdoc />
        protected override string ModifierClass => "polhem-field-editor--month";

        /// <inheritdoc />
        protected override CalendarViews View => CalendarViews.Months;
    }
}
