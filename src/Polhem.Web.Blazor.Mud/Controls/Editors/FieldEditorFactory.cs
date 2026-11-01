using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Mud.Controls.Editors
{
    /// <summary>
    /// Maps a <see cref="ControlType"/> to the MudBlazor field editor that renders it.
    /// </summary>
    public static class FieldEditorFactory
    {
        /// <summary>
        /// Gets the field editor component type for <paramref name="controlType"/>. <see cref="ControlType.Auto"/> and
        /// any unmapped value fall back to <see cref="TextEdit"/>.
        /// </summary>
        /// <param name="controlType">The layout control type.</param>
        /// <returns>The component type.</returns>
        public static Type GetEditorType(ControlType controlType) => controlType switch
        {
            ControlType.CheckEdit => typeof(CheckEdit),
            ControlType.DateEdit => typeof(DateEdit),
            ControlType.YearMonthEdit => typeof(YearMonthEdit),
            ControlType.MemoEdit => typeof(MemoEdit),
            ControlType.DropDownEdit => typeof(DropDownEdit),
            ControlType.NumericEdit => typeof(NumericEdit),
            ControlType.TimeEdit => typeof(TimeEdit),
            ControlType.DateTimeEdit => typeof(DateTimeEdit),
            _ => typeof(TextEdit),
        };
    }
}
