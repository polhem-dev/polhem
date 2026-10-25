using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Maps a <see cref="ControlType"/> to the field editor component that renders it, the same mapping as
    /// the Avalonia head's <c>FieldEditorFactory</c>.
    /// </summary>
    /// <remarks>
    /// Blazor creates components itself, so this returns the component type for a
    /// <c>DynamicComponent</c> or a render tree builder instead of an instance.
    /// </remarks>
    public static class FieldEditorFactory
    {
        /// <summary>
        /// Gets the field editor component type for <paramref name="controlType"/>. Every returned type
        /// derives from <see cref="FieldEditorBase"/>; <see cref="ControlType.Auto"/> and any unmapped value
        /// fall back to <see cref="TextEdit"/>.
        /// </summary>
        /// <param name="controlType">The layout control type.</param>
        /// <returns>The component type.</returns>
        public static Type GetEditorType(ControlType controlType)
        {
            return controlType switch
            {
                ControlType.CheckEdit => typeof(CheckEdit),
                ControlType.DateEdit => typeof(DateEdit),
                ControlType.YearMonthEdit => typeof(YearMonthEdit),
                ControlType.MemoEdit => typeof(MemoEdit),
                ControlType.DropDownEdit => typeof(DropDownEdit),
                ControlType.ButtonEdit => typeof(ButtonEdit),
                ControlType.NumericEdit => typeof(NumericEdit),
                ControlType.TimeEdit => typeof(TimeEdit),
                ControlType.DateTimeEdit => typeof(DateTimeEdit),
                _ => typeof(TextEdit),
            };
        }
    }
}
