using Bunit;
using Bunit.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Fluent.Components;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared saved-query behaviors on the Fluent UI form page. Its menu and dialogs render in place.
    /// </summary>
    public class FluentUserQueryBehaviorTests : UserQueryBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentUserQueryBehaviorTests"/>.
        /// </summary>
        public FluentUserQueryBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type PageType => typeof(FormPage);

        /// <inheritdoc />
        protected override string RowSelector => "tr.polhem-grid__row";

        /// <inheritdoc />
        protected override void ChooseQuery(IRenderedComponent<ContainerFragment> cut, Guid rowId)
        {
            var picker = cut.FindComponent<FluentSelect<Guid, Guid>>();
            picker.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync(rowId));
        }

        /// <inheritdoc />
        protected override IReadOnlyDictionary<string, bool> ActionStates(IRenderedComponent<ContainerFragment> cut)
        {
            OpenMenu(cut);
            var states = cut.FindAll(".polhem-menu__item")
                .Select(i => (Class: i.ClassList.First(c => c.StartsWith("polhem-user-query-bar__", StringComparison.Ordinal)), Enabled: !i.HasAttribute("disabled")))
                .ToDictionary(i => i.Class, i => i.Enabled, StringComparer.Ordinal);
            cut.Find(".polhem-user-query-bar__actions button").Click();
            return states;
        }

        /// <inheritdoc />
        protected override void ChooseAction(IRenderedComponent<ContainerFragment> cut, string action)
        {
            OpenMenu(cut);
            cut.Find($".{action}").Click();
        }

        /// <inheritdoc />
        protected override void AnswerNamePrompt(IRenderedComponent<ContainerFragment> cut, string name, bool checkOption)
        {
            cut.WaitForAssertion(() => cut.Find(".polhem-name-prompt-dialog__name input").Input(name));
            if (checkOption) cut.Find(".polhem-name-prompt-dialog__option input").Change(true);
            cut.Find(".polhem-name-prompt-dialog__form").Submit();
        }

        /// <inheritdoc />
        protected override void Confirm(IRenderedComponent<ContainerFragment> cut)
            => cut.WaitForAssertion(() => cut.Find(".polhem-confirm-dialog__confirm").Click());

        /// <inheritdoc />
        protected override void TypeValue(IRenderedComponent<ContainerFragment> cut, int row, string text)
            => cut.FindAll(".polhem-criteria-row")[row].QuerySelector(".polhem-criteria-row__value input, .polhem-criteria-row__from input")!.Change(text);

        private static void OpenMenu(IRenderedComponent<ContainerFragment> cut)
        {
            cut.Find(".polhem-user-query-bar__actions button").Click();
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".polhem-user-query-bar__save-as")));
        }
    }
}
