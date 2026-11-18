using Bunit;
using Bunit.Rendering;
using MudBlazor;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Mud.Components;
using Polhem.Web.Blazor.Mud.Controls;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.UnitTests
{
    /// <summary>
    /// Runs the shared query pane behaviors on the MudBlazor form page. Its menus render in the popover provider.
    /// </summary>
    public class MudQueryPaneBehaviorTests : QueryPaneBehaviorTests
    {
        private IRenderedComponent<MudPopoverProvider> _popovers = default!;

        /// <summary>
        /// Initializes a new instance of <see cref="MudQueryPaneBehaviorTests"/>.
        /// </summary>
        public MudQueryPaneBehaviorTests()
        {
            Services.AddPolhemMud();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override void OnServicesRegistered() => _popovers = Render<MudPopoverProvider>();

        /// <inheritdoc />
        protected override Type PageType => typeof(FormPage);

        /// <inheritdoc />
        protected override Type PanelType => typeof(CriteriaPanel);

        /// <inheritdoc />
        protected override string RowSelector => "tr.polhem-grid__row";

        /// <inheritdoc />
        protected override string CardSelector => ".polhem-card";

        /// <inheritdoc />
        protected override void AddCondition(IRenderedComponent<ContainerFragment> cut, string caption)
        {
            int rows = cut.FindAll(".polhem-criteria-row").Count;
            cut.Find(".polhem-criteria-panel__add button").Click();
            _popovers.WaitForAssertion(() => _popovers.FindAll(".polhem-criteria-panel__field")
                .Single(i => i.TextContent.Trim() == caption).Click());
            cut.WaitForAssertion(() => Assert.Equal(rows + 1, cut.FindAll(".polhem-criteria-row").Count));
        }

        /// <inheritdoc />
        protected override void TypeValue(IRenderedComponent<ContainerFragment> cut, int row, string text)
            => cut.FindAll(".polhem-criteria-row")[row].QuerySelector(".polhem-criteria-row__value input, .polhem-criteria-row__from input")!.Input(text);

        /// <inheritdoc />
        protected override void CheckChoice(IRenderedComponent<ContainerFragment> cut, int row, string text)
        {
            var choice = cut.FindAll(".polhem-criteria-row")[row].QuerySelectorAll(".polhem-criteria-row__choice")
                .Single(c => c.TextContent.Contains(text, StringComparison.Ordinal));
            choice.QuerySelector("input")!.Change(true);
        }

        /// <inheritdoc />
        protected override void ChoosePeriod(IRenderedComponent<ContainerFragment> cut, int row, string text)
        {
            cut.FindAll(".polhem-criteria-row")[row].QuerySelector(".polhem-criteria-row__period button")!.Click();
            _popovers.WaitForAssertion(() => _popovers.FindAll(".polhem-criteria-row__period-item")
                .Single(i => i.TextContent.Trim() == text).Click());
        }

        /// <inheritdoc />
        protected override void ApplyFirstColumnFilter(IRenderedComponent<ContainerFragment> cut, string text)
        {
            cut.Find(".polhem-grid-filter__button").Click();
            _popovers.WaitForAssertion(() => _popovers.Find(".polhem-grid-filter__first input").Input(text));
            _popovers.Find(".polhem-grid-filter__form").Submit();
        }

        /// <inheritdoc />
        protected override void NextPage(IRenderedComponent<ContainerFragment> cut)
            => cut.FindAll(".polhem-form-page__pager button")
                .Single(b => b.TextContent.Trim() == PolhemUIText.Get(PolhemBlazorText.GetLocalizer(null), PolhemUIText.NextPage))
                .Click();
    }
}
