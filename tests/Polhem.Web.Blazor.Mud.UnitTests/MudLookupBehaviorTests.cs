using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Controls.Editors;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared lookup behaviors on the MudBlazor lookup editor. Its picker is a dialog, which renders in the
    /// dialog provider rather than in the editor's markup.
    /// </summary>
    public class MudLookupBehaviorTests : LookupBehaviorTests
    {
        private const string Input = ".polhem-field-editor--button input";
        private const string Row = "tr.polhem-grid__row";
        private const string Button = ".polhem-field-editor--button .mud-input-adornment button";

        private readonly IRenderedComponent<MudDialogProvider> _dialogs;

        /// <summary>
        /// Initializes a new instance of <see cref="MudLookupBehaviorTests"/>.
        /// </summary>
        public MudLookupBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
            Render<MudPopoverProvider>();
            _dialogs = Render<MudDialogProvider>();
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
        protected override void PressKey(IRenderedComponent<ContainerFragment> cut, string key) => cut.Find(Input).KeyDown(key);

        /// <inheritdoc />
        protected override void DoubleClickEditor(IRenderedComponent<ContainerFragment> cut) => cut.Find(".polhem-button-edit").DoubleClick();

        /// <inheritdoc />
        protected override bool IsPickerOpen(IRenderedComponent<ContainerFragment> cut) => _dialogs.FindAll(".polhem-lookup-panel").Count > 0;

        /// <inheritdoc />
        protected override int PickerRowCount(IRenderedComponent<ContainerFragment> cut) => _dialogs.FindAll(Row).Count;

        /// <inheritdoc />
        protected override void ClickPickerRow(IRenderedComponent<ContainerFragment> cut, int row) => _dialogs.FindAll(Row)[row].Click();

        /// <inheritdoc />
        protected override void DoubleClickPickerRow(IRenderedComponent<ContainerFragment> cut, int row) => _dialogs.FindAll(Row)[row].Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Detail = 2 });

        /// <inheritdoc />
        protected override bool CanConfirmPicker(IRenderedComponent<ContainerFragment> cut) => !_dialogs.Find(".polhem-lookup-panel__ok").HasAttribute("disabled");

        /// <inheritdoc />
        protected override void ConfirmPicker(IRenderedComponent<ContainerFragment> cut) => _dialogs.Find(".polhem-lookup-panel__ok").Click();

        /// <inheritdoc />
        protected override void CancelPicker(IRenderedComponent<ContainerFragment> cut) => _dialogs.Find(".polhem-lookup-panel__cancel").Click();

        /// <inheritdoc />
        protected override IReadOnlyList<string> PickerText(IRenderedComponent<ContainerFragment> cut, string selector)
            => _dialogs.FindAll(selector).Select(e => e.TextContent).ToList();

        /// <inheritdoc />
        protected override void SearchPicker(IRenderedComponent<ContainerFragment> cut, string text)
        {
            _dialogs.Find(".polhem-lookup-panel__search-input input").Change(text);
            _dialogs.Find(".polhem-lookup-panel__search").Submit();
        }
    }
}
