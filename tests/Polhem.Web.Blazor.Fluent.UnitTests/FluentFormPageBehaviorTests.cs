using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.FluentUI.AspNetCore.Components;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Components;
using Polhem.Web.Blazor.Fluent.Components;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.UnitTests
{
    /// <summary>
    /// Runs the shared form page behaviors on the Fluent UI form page. Its dialogs are inline, in the page's markup.
    /// </summary>
    public class FluentFormPageBehaviorTests : FormPageBehaviorTests
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentFormPageBehaviorTests"/>.
        /// </summary>
        public FluentFormPageBehaviorTests()
        {
            Services.AddPolhemFluent();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        /// <inheritdoc />
        protected override Type PageType => typeof(FormPage);

        /// <inheritdoc />
        protected override string Toolbar => ".polhem-form-page__toolbar fluent-button";

        /// <inheritdoc />
        protected override string Pager => ".polhem-form-page__pager fluent-button";

        /// <inheritdoc />
        protected override string RelatedRecordButton => ".polhem-related-record-dialog__toolbar fluent-button";

        /// <inheritdoc />
        protected override string RowSelector => "tr.polhem-grid__row";

        /// <inheritdoc />
        protected override string SortSelector => "button.polhem-grid__sort";

        /// <inheritdoc />
        protected override void DoubleClickRow(IElement row)
        {
            // A grid row has a double-click event, and a card reads the browser's click count.
            if (row.LocalName == "tr") row.DoubleClick();
            else row.Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Detail = 2 });
        }

        /// <inheritdoc />
        protected override void DoubleClickField(IRenderedComponent<ContainerFragment> cut, string fieldName)
            => cut.Find($"#polhem-form-{fieldName}").Closest(".polhem-button-edit")!.DoubleClick();

        /// <inheritdoc />
        protected override IReadOnlyList<IElement> FindInDialogs(IRenderedComponent<ContainerFragment> cut, string selector)
            => cut.FindAll(selector);

        /// <inheritdoc />
        protected override void ReachCardListEnd(IRenderedComponent<ContainerFragment> cut)
        {
            var end = cut.FindComponent<CardListEnd>();
            end.InvokeAsync(() => end.Instance.ReachedEnd.InvokeAsync());
        }

        /// <inheritdoc />
        protected override void ApplyFirstColumnFilter(IRenderedComponent<ContainerFragment> cut, string text)
        {
            cut.Find(".polhem-grid-filter__button").Click();
            cut.WaitForAssertion(() => cut.Find(".polhem-grid-filter__first fluent-text-input").Change(text));
            cut.Find(".polhem-grid-filter__form").Submit();
        }

        /// <inheritdoc />
        protected override void ChoosePageSize(IRenderedComponent<ContainerFragment> cut, int pageSize)
        {
            var select = cut.FindComponent<FluentSelect<int, int>>();
            select.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(pageSize));
        }
    }
}
