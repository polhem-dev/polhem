using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Behavior
{
    /// <summary>
    /// Runs the shared form page behaviors on the HTML form page.
    /// </summary>
    public class HtmlFormPageBehaviorTests : FormPageBehaviorTests
    {
        /// <inheritdoc />
        protected override Type PageType => typeof(FormPage);

        /// <inheritdoc />
        protected override string RowSelector => "tr.polhem-dynamic-grid__row";

        /// <inheritdoc />
        protected override string SortSelector => "button.polhem-dynamic-grid__sort";

        /// <inheritdoc />
        protected override void DoubleClickRow(IElement row) => row.DoubleClick();

        /// <inheritdoc />
        protected override void DoubleClickField(IRenderedComponent<ContainerFragment> cut, string fieldName)
            => cut.Find($"input#polhem-form-{fieldName}").DoubleClick();

        /// <inheritdoc />
        protected override IReadOnlyList<IElement> FindInDialogs(IRenderedComponent<ContainerFragment> cut, string selector)
            => cut.FindAll(selector);

        /// <inheritdoc />
        protected override void ChoosePageSize(IRenderedComponent<ContainerFragment> cut, int pageSize)
            => cut.Find(".polhem-form-page__page-size select").Change(pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
