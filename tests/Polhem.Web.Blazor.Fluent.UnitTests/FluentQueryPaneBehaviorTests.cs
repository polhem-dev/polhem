using Bunit;
using Bunit.Rendering;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Fluent.Components;
using Polhem.Web.Blazor.Fluent.Controls;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared query pane behaviors on the Fluent UI form page. Its values are native inputs that write back on
    /// change, and its menus render in place.
    /// </summary>
    public class FluentQueryPaneBehaviorTests : QueryPaneBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentQueryPaneBehaviorTests"/>.
        /// </summary>
        public FluentQueryPaneBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

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
            cut.WaitForAssertion(() => cut.FindAll(".polhem-criteria-panel__field").Single(i => i.TextContent.Trim() == caption).Click());
            cut.WaitForAssertion(() => Assert.Equal(rows + 1, cut.FindAll(".polhem-criteria-row").Count));
        }

        /// <inheritdoc />
        protected override void TypeValue(IRenderedComponent<ContainerFragment> cut, int row, string text)
            => cut.FindAll(".polhem-criteria-row")[row].QuerySelector(".polhem-criteria-row__value input, .polhem-criteria-row__from input")!.Change(text);

        /// <inheritdoc />
        protected override void CheckChoice(IRenderedComponent<ContainerFragment> cut, int row, string text)
            => cut.FindAll(".polhem-criteria-row")[row].QuerySelectorAll(".polhem-criteria-row__choice")
                .Single(c => c.TextContent.Contains(text, StringComparison.Ordinal))
                .QuerySelector("input")!.Change(true);

        /// <inheritdoc />
        protected override void ChoosePeriod(IRenderedComponent<ContainerFragment> cut, int row, string text)
        {
            cut.FindAll(".polhem-criteria-row")[row].QuerySelector(".polhem-criteria-row__period button")!.Click();
            cut.WaitForAssertion(() => cut.FindAll(".polhem-criteria-row__period-item").Single(i => i.TextContent.Trim() == text).Click());
        }

        /// <inheritdoc />
        protected override void ApplyFirstColumnFilter(IRenderedComponent<ContainerFragment> cut, string text)
        {
            cut.Find(".polhem-grid-filter__button").Click();
            cut.WaitForAssertion(() => cut.Find(".polhem-grid-filter__first input").Change(text));
            cut.Find(".polhem-grid-filter__form").Submit();
        }

        /// <inheritdoc />
        protected override void NextPage(IRenderedComponent<ContainerFragment> cut)
            => cut.FindAll(".polhem-form-page__pager fluent-button")
                .Single(b => b.TextContent.Trim() == PolhemUIText.Get(PolhemBlazorText.GetLocalizer(null), PolhemUIText.NextPage))
                .Click();
    }
}
