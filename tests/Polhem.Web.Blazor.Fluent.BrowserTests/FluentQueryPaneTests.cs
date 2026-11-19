using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared query pane browser tests on the Fluent UI package.
    /// </summary>
    public class FluentQueryPaneTests : QueryPaneTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentQueryPaneTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentQueryPaneTests(FluentSiteFixture fixture) : base(fixture)
        {
        }
    }
}
