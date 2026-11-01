using Bunit;
using Bunit.Rendering;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Server.Controls.Editors;

namespace Polhem.Web.Blazor.Server.UnitTests.Behavior
{
    /// <summary>
    /// Runs the shared editor behaviors on the HTML editors.
    /// </summary>
    public class HtmlEditorBehaviorTests : EditorBehaviorTests
    {
        private static string Selector(ControlType controlType) => controlType switch
        {
            ControlType.MemoEdit => ".polhem-field-editor--memo",
            ControlType.NumericEdit => ".polhem-field-editor--numeric",
            ControlType.DateEdit => ".polhem-field-editor--date",
            ControlType.TimeEdit => ".polhem-field-editor--time",
            ControlType.DateTimeEdit => ".polhem-field-editor--datetime",
            ControlType.DropDownEdit => ".polhem-field-editor--select",
            ControlType.CheckEdit => ".polhem-field-editor--checkbox",
            _ => ".polhem-field-editor--text",
        };

        /// <inheritdoc />
        protected override Type EditorType(ControlType controlType) => FieldEditorFactory.GetEditorType(controlType);

        /// <inheritdoc />
        protected override string ReadText(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            var element = cut.Find(Selector(controlType));
            return controlType switch
            {
                ControlType.MemoEdit => element.GetAttribute("value") ?? element.TextContent,
                ControlType.DropDownEdit => element.QuerySelector("option[value='']") is not null ? string.Empty : element.GetAttribute("value") ?? string.Empty,
                _ => element.GetAttribute("value") ?? string.Empty,
            };
        }

        /// <inheritdoc />
        protected override void Enter(IRenderedComponent<ContainerFragment> cut, ControlType controlType, string text)
            => cut.Find(Selector(controlType)).Change(text);

        /// <inheritdoc />
        protected override bool AcceptsInput(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            var element = cut.Find(Selector(controlType));
            return !element.HasAttribute("readonly") && !element.HasAttribute("disabled");
        }

        /// <inheritdoc />
        protected override void FocusNumeric(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(Selector(ControlType.NumericEdit)).Focus();

        /// <inheritdoc />
        protected override void BlurNumeric(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(Selector(ControlType.NumericEdit)).Blur();

        /// <inheritdoc />
        protected override bool IsChecked(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(Selector(ControlType.CheckEdit)).HasAttribute("checked");

        /// <inheritdoc />
        protected override void SetChecked(IRenderedComponent<ContainerFragment> cut, bool value)
            => cut.Find(Selector(ControlType.CheckEdit)).Change(value);
    }
}
