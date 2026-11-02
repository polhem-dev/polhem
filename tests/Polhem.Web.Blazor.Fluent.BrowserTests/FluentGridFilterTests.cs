using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared column filter tests on the Fluent UI UI package.
    /// </summary>
    public class FluentGridFilterTests : GridFilterTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridFilterTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentGridFilterTests(FluentSiteFixture fixture) : base(fixture)
        {
        }
    }
}
