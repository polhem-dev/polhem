using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Controls.Editors;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared editor behaviors on the MudBlazor editors.
    /// </summary>
    public class MudEditorBehaviorTests : EditorBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudEditorBehaviorTests"/>.
        /// </summary>
        public MudEditorBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
            Render<MudPopoverProvider>();
        }

        private static string Selector(ControlType controlType) => controlType switch
        {
            ControlType.MemoEdit => ".polhem-field-editor--memo textarea",
            ControlType.NumericEdit => ".polhem-field-editor--numeric input",
            ControlType.DateEdit => ".polhem-field-editor--date input",
            ControlType.YearMonthEdit => ".polhem-field-editor--month input",
            ControlType.TimeEdit => ".polhem-field-editor--time input",
            ControlType.DropDownEdit => ".polhem-field-editor--select input",
            ControlType.CheckEdit => ".polhem-field-editor--checkbox input",
            ControlType.DateTimeEdit => ".polhem-field-editor--datetime input",
            _ => ".polhem-field-editor--text input",
        };

        /// <inheritdoc />
        protected override Type EditorType(ControlType controlType) => FieldEditorFactory.GetEditorType(controlType);

        /// <inheritdoc />
        protected override string ReadText(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            if (controlType == ControlType.DateTimeEdit)
            {
                var inputs = cut.FindAll(Selector(controlType));
                return $"{inputs[0].GetAttribute("value")} {inputs[1].GetAttribute("value")}";
            }
            var element = cut.Find(Selector(controlType));
            return element.GetAttribute("value") ?? element.TextContent;
        }

        /// <inheritdoc />
        protected override void Enter(IRenderedComponent<ContainerFragment> cut, ControlType controlType, string text)
        {
            if (controlType == ControlType.DropDownEdit)
            {
                var select = cut.FindComponent<MudSelect<string>>();
                cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(text)).GetAwaiter().GetResult();
                return;
            }
            if (controlType == ControlType.DateTimeEdit)
            {
                var parts = text.Split(' ');
                cut.FindAll(Selector(controlType))[0].Change(parts[0]);
                cut.FindAll(Selector(controlType))[1].Change(parts[1]);
                return;
            }
            cut.Find(Selector(controlType)).Change(text);
        }

        // MudBlazor draws the read-only look with its text variant, which keeps only the underline.
        /// <inheritdoc />
        protected override bool HasReadOnlyLook(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            var inputs = cut.FindAll(".mud-input");
            return inputs.Count > 0 && inputs.All(input => input.ClassList.Contains("mud-input-text"));
        }

        /// <inheritdoc />
        protected override bool AcceptsInput(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            // A select's input is read-only either way, and a check box refuses a click rather than marking its input.
            if (controlType == ControlType.DropDownEdit) return cut.FindComponents<MudSelect<string>>().Count > 0;
            if (controlType == ControlType.CheckEdit) return !cut.FindComponent<MudCheckBox<bool>>().Instance.ReadOnly;
            var element = cut.Find(Selector(controlType));
            return !element.HasAttribute("readonly") && !element.HasAttribute("disabled");
        }

        /// <inheritdoc />
        protected override void FocusNumeric(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(".polhem-field-editor--numeric").ParentElement!.TriggerEvent("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

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
