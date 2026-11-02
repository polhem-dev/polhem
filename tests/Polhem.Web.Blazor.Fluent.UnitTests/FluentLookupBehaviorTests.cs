using Bunit;
using Bunit.Rendering;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.Controls.Editors;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared lookup behaviors on the Fluent UI lookup editor. Its picker is an inline dialog, in the editor's markup.
    /// </summary>
    public class FluentLookupBehaviorTests : LookupBehaviorTests
    {
        private const string Input = ".polhem-field-editor--button fluent-text-input";
        private const string Row = "tr.polhem-grid__row";
        private const string Button = ".polhem-button-edit__search";

        /// <summary>
        /// Initializes a new instance of <see cref="FluentLookupBehaviorTests"/>.
        /// </summary>
        public FluentLookupBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type EditorType => typeof(ButtonEdit);

        /// <inheritdoc />
        protected override string ReadText(IRenderedComponent<ContainerFragment> cut) => cut.Find(Input).GetAttribute("value") ?? string.Empty;

        /// <inheritdoc />
        protected override bool AcceptsTyping(IRenderedComponent<ContainerFragment> cut) => !cut.Find(Input).HasAttribute("readonly");

        /// <inheritdoc />
        protected override bool ShowsButton(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(Button).Count > 0;

        /// <inheritdoc />
        protected override void ClickButton(IRenderedComponent<ContainerFragment> cut) => cut.Find(Button).Click();

        /// <inheritdoc />
        protected override void PressKey(IRenderedComponent<ContainerFragment> cut, string key) => cut.Find(".polhem-button-edit").KeyDown(key);

        /// <inheritdoc />
        protected override void DoubleClickEditor(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-button-edit").DoubleClick();

        /// <inheritdoc />
        protected override bool IsPickerOpen(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(".polhem-lookup-panel").Count > 0;

        /// <inheritdoc />
        protected override int PickerRowCount(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(Row).Count;

        /// <inheritdoc />
        protected override void ClickPickerRow(IRenderedComponent<ContainerFragment> cut, int row) => cut.FindAll(Row)[row].Click();

        /// <inheritdoc />
        protected override void DoubleClickPickerRow(IRenderedComponent<ContainerFragment> cut, int row) => cut.FindAll(Row)[row].DoubleClick();

        /// <inheritdoc />
        protected override bool CanConfirmPicker(IRenderedComponent<ContainerFragment> cut) => !cut.Find(".polhem-lookup-panel__ok").HasAttribute("disabled");

        /// <inheritdoc />
        protected override void ConfirmPicker(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-lookup-panel__ok").Click();

        /// <inheritdoc />
        protected override void CancelPicker(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-lookup-panel__cancel").Click();

        /// <inheritdoc />
        protected override IReadOnlyList<string> PickerText(IRenderedComponent<ContainerFragment> cut, string selector)
            => cut.FindAll(selector).Select(e => e.TextContent).ToList();

        /// <inheritdoc />
        protected override void SearchPicker(IRenderedComponent<ContainerFragment> cut, string text)
        {
            cut.Find(".polhem-lookup-panel__search-input fluent-text-input").Change(text);
            cut.Find(".polhem-lookup-panel__search").Submit();
        }
    }
}
