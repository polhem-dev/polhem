using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared form layout alignment tests on the Fluent UI UI package.
    /// </summary>
    public class FluentFormLayoutAlignmentTests : FormLayoutAlignmentTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentFormLayoutAlignmentTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentFormLayoutAlignmentTests(FluentSiteFixture fixture) : base(fixture)
        {
        }

        /// <inheritdoc />
        protected override string EnabledCommandButton => ".polhem-form-page__toolbar fluent-button:not([disabled])";
    }
}
