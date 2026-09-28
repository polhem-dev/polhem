using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using Polhem.Definition.Layouts;
using Polhem.UI.Avalonia.Controls.Editors;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Editors
{
    /// <summary>
    /// Verifies the <see cref="FieldEditorFactory"/> ControlType dispatch and the
    /// StyleKey contract that keeps every editor on its native control's theme.
    /// </summary>
    public class FieldEditorFactoryTests
    {
        [Theory]
        [InlineData(ControlType.TextEdit, typeof(TextEdit))]
        [InlineData(ControlType.MemoEdit, typeof(MemoEdit))]
        [InlineData(ControlType.ButtonEdit, typeof(ButtonEdit))]
        [InlineData(ControlType.DateEdit, typeof(DateEdit))]
        [InlineData(ControlType.YearMonthEdit, typeof(YearMonthEdit))]
        [InlineData(ControlType.DropDownEdit, typeof(DropDownEdit))]
        [InlineData(ControlType.CheckEdit, typeof(CheckEdit))]
        [InlineData(ControlType.NumericEdit, typeof(NumericEdit))]
        [InlineData(ControlType.TimeEdit, typeof(TimeEdit))]
        [InlineData(ControlType.DateTimeEdit, typeof(DateTimeEdit))]
        [InlineData(ControlType.Auto, typeof(TextEdit))]
        [DisplayName("FieldEditorFactory creates the editor matching ControlType (Auto falls back to TextEdit)")]
        public void Create_ControlType_ReturnsMatchingEditor(ControlType controlType, Type expectedType)
        {
            var editor = FieldEditorFactory.Create(controlType);

            Assert.IsType(expectedType, editor);
            Assert.IsType<IFieldEditor>(editor, exactMatch: false);
        }

        [Theory]
        [InlineData(typeof(TextEdit), typeof(TextBox))]
        [InlineData(typeof(MemoEdit), typeof(TextBox))]
        [InlineData(typeof(ButtonEdit), typeof(TextBox))]
        [InlineData(typeof(DateEdit), typeof(DatePicker))]
        [InlineData(typeof(YearMonthEdit), typeof(DatePicker))]
        [InlineData(typeof(DropDownEdit), typeof(ComboBox))]
        [InlineData(typeof(CheckEdit), typeof(CheckBox))]
        [InlineData(typeof(NumericEdit), typeof(TextBox))]
        [InlineData(typeof(DateTimeEdit), typeof(TextBox))]
        [DisplayName("Each editor's StyleKeyOverride points to its native base (guards against invisible-control regressions)")]
        public void StyleKeyOverride_Editor_PointsToNativeBase(Type editorType, Type expectedStyleKey)
        {
            var editor = (Control)Activator.CreateInstance(editorType)!;
            var styleKey = typeof(global::Avalonia.StyledElement)
                .GetProperty("StyleKeyOverride", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(editor);

            Assert.Equal(expectedStyleKey, styleKey);
        }
    }
}
