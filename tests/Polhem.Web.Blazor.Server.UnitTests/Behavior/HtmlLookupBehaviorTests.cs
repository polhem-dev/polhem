using Bunit;
using Bunit.Rendering;
using Polhem.Web.Blazor.Server.Controls.Editors;

namespace Polhem.Web.Blazor.Server.UnitTests.Behavior
{
    /// <summary>
    /// Runs the shared lookup behaviors on the HTML lookup editor.
    /// </summary>
    public class HtmlLookupBehaviorTests : LookupBehaviorTests
    {
        private const string Input = "input.polhem-field-editor--button";
        private const string Row = "tr.polhem-dynamic-grid__row";

        /// <inheritdoc />
        protected override Type EditorType => typeof(ButtonEdit);

        /// <inheritdoc />
        protected override string ReadText(IRenderedComponent<ContainerFragment> cut) => cut.Find(Input).GetAttribute("value") ?? string.Empty;

        /// <inheritdoc />
        protected override bool AcceptsTyping(IRenderedComponent<ContainerFragment> cut) => !cut.Find(Input).HasAttribute("readonly");

        /// <inheritdoc />
        protected override bool ShowsButton(IRenderedComponent<ContainerFragment> cut) => cut.FindAll("button.polhem-button-edit__button").Count > 0;

        /// <inheritdoc />
        protected override void ClickButton(IRenderedComponent<ContainerFragment> cut) => cut.Find("button.polhem-button-edit__button").Click();

        /// <inheritdoc />
        protected override void PressKey(IRenderedComponent<ContainerFragment> cut, string key) => cut.Find(Input).KeyDown(key);

        /// <inheritdoc />
        protected override void DoubleClickEditor(IRenderedComponent<ContainerFragment> cut) => cut.Find(Input).DoubleClick();

        /// <inheritdoc />
        protected override bool IsPickerOpen(IRenderedComponent<ContainerFragment> cut) => cut.FindAll(".polhem-lookup-dialog").Count > 0;

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
            cut.Find(".polhem-lookup-panel__search-input").Change(text);
            cut.Find(".polhem-lookup-panel__search").Submit();
        }
    }
}
