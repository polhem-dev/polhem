using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.Components;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared saved-query behaviors on the MudBlazor form page. Its menus render in the popover provider and its
    /// dialogs in the dialog provider.
    /// </summary>
    public class MudUserQueryBehaviorTests : UserQueryBehaviorTests
    {
        private IRenderedComponent<MudPopoverProvider> _popovers = default!;
        private IRenderedComponent<MudDialogProvider> _dialogs = default!;

        /// <summary>
        /// Initializes a new instance of <see cref="MudUserQueryBehaviorTests"/>.
        /// </summary>
        public MudUserQueryBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override void OnServicesRegistered()
        {
            _popovers = Render<MudPopoverProvider>();
            _dialogs = Render<MudDialogProvider>();
        }

        /// <inheritdoc />
        protected override Type PageType => typeof(FormPage);

        /// <inheritdoc />
        protected override string RowSelector => "tr.polhem-grid__row";

        /// <inheritdoc />
        protected override void ChooseQuery(IRenderedComponent<ContainerFragment> cut, Guid rowId)
        {
            var picker = cut.FindComponent<MudSelect<Guid>>();
            picker.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(rowId));
        }

        /// <inheritdoc />
        protected override IReadOnlyDictionary<string, bool> ActionStates(IRenderedComponent<ContainerFragment> cut)
        {
            OpenMenu(cut);
            var states = _popovers.FindComponents<MudMenuItem>()
                .Where(i => i.Instance.Class?.StartsWith("polhem-user-query-bar__", StringComparison.Ordinal) == true)
                .ToDictionary(i => i.Instance.Class!, i => !i.Instance.Disabled, StringComparer.Ordinal);
            // The menu stays open in the popover provider; a click on its own button closes it.
            cut.Find(".polhem-user-query-bar__actions button").Click();
            return states;
        }

        /// <inheritdoc />
        protected override void ChooseAction(IRenderedComponent<ContainerFragment> cut, string action)
        {
            OpenMenu(cut);
            _popovers.Find($".{action}").Click();
        }

        /// <inheritdoc />
        protected override void AnswerNamePrompt(IRenderedComponent<ContainerFragment> cut, string name, bool checkOption)
        {
            _dialogs.WaitForAssertion(() => _dialogs.Find(".polhem-name-prompt-dialog__name input").Input(name));
            if (checkOption) _dialogs.Find(".polhem-name-prompt-dialog__option input").Change(true);
            _dialogs.Find(".polhem-name-prompt-dialog__form").Submit();
        }

        /// <inheritdoc />
        protected override void Confirm(IRenderedComponent<ContainerFragment> cut)
            => _dialogs.WaitForAssertion(() => _dialogs.Find(".polhem-confirm-dialog__confirm").Click());

        /// <inheritdoc />
        protected override void TypeValue(IRenderedComponent<ContainerFragment> cut, int row, string text)
            => cut.FindAll(".polhem-criteria-row")[row].QuerySelector(".polhem-criteria-row__value input, .polhem-criteria-row__from input")!.Input(text);

        private void OpenMenu(IRenderedComponent<ContainerFragment> cut)
        {
            cut.Find(".polhem-user-query-bar__actions button").Click();
            _popovers.WaitForAssertion(() => Assert.NotEmpty(_popovers.FindAll(".polhem-user-query-bar__save-as")));
        }
    }
}
