using Bunit;
using Bunit.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;
using Polhem.Definition.Collections;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.Controls.Editors;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared editor behaviors on the Fluent UI editors. bUnit runs no web component, so a test talks to the custom
    /// element itself, where the component binds its events.
    /// </summary>
    public class FluentEditorBehaviorTests : EditorBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentEditorBehaviorTests"/>.
        /// </summary>
        public FluentEditorBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        private static string Selector(ControlType controlType) => controlType switch
        {
            ControlType.MemoEdit => ".polhem-field-editor--memo fluent-textarea, fluent-textarea.polhem-field-editor--memo",
            ControlType.NumericEdit => ".polhem-field-editor--numeric fluent-text-input",
            ControlType.DateEdit => ".polhem-field-editor--date fluent-text-input",
            ControlType.YearMonthEdit => ".polhem-field-editor--month fluent-text-input",
            ControlType.TimeEdit => ".polhem-field-editor--time fluent-text-input",
            ControlType.DropDownEdit => ".polhem-field-editor--select fluent-dropdown, .polhem-field-editor--select fluent-text-input",
            ControlType.CheckEdit => "fluent-checkbox",
            ControlType.DateTimeEdit => ".polhem-field-editor--datetime fluent-text-input",
            _ => ".polhem-field-editor--text fluent-text-input",
        };

        /// <inheritdoc />
        protected override Type EditorType(ControlType controlType) => FieldEditorFactory.GetEditorType(controlType);

        /// <inheritdoc />
        protected override string ReadText(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            if (controlType == ControlType.DateTimeEdit)
            {
                var inputs = cut.FindAll(Selector(controlType));
                if (inputs.Count == 1) return inputs[0].GetAttribute("value") ?? string.Empty;
                return $"{inputs[0].GetAttribute("value")} {inputs[1].GetAttribute("value")}";
            }
            var element = cut.Find(Selector(controlType));
            if (element.LocalName == "fluent-dropdown")
                return element.QuerySelector("fluent-option[selected]")?.TextContent ?? string.Empty;
            return element.GetAttribute("value") ?? element.TextContent;
        }

        /// <inheritdoc />
        protected override void Enter(IRenderedComponent<ContainerFragment> cut, ControlType controlType, string text)
        {
            if (controlType == ControlType.DropDownEdit)
            {
                var select = cut.FindComponent<FluentSelect<ListItem, string>>();
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

        // The Fluent stylesheet draws the read-only look on a text input or a text area that carries `readonly`.
        /// <inheritdoc />
        protected override bool HasReadOnlyLook(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            var inputs = cut.FindAll("fluent-text-input, fluent-textarea");
            return inputs.Count > 0 && inputs.All(input => input.HasAttribute("readonly"));
        }

        /// <inheritdoc />
        protected override bool AcceptsInput(IRenderedComponent<ContainerFragment> cut, ControlType controlType)
        {
            if (controlType == ControlType.DropDownEdit) return cut.FindComponents<FluentSelect<ListItem, string>>().Count > 0;
            var element = cut.Find(Selector(controlType));
            return !element.HasAttribute("readonly") && !element.HasAttribute("disabled");
        }

        /// <inheritdoc />
        protected override void FocusNumeric(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(".polhem-field-editor--numeric").ParentElement!.TriggerEvent("onfocusin", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        /// <inheritdoc />
        protected override void BlurNumeric(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(".polhem-field-editor--numeric").ParentElement!.TriggerEvent("onfocusout", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        /// <inheritdoc />
        protected override bool IsChecked(IRenderedComponent<ContainerFragment> cut)
            => cut.Find(Selector(ControlType.CheckEdit)).HasAttribute("checked");

        /// <inheritdoc />
        protected override void SetChecked(IRenderedComponent<ContainerFragment> cut, bool value)
            => cut.Find(Selector(ControlType.CheckEdit)).Change(value);
    }
}
