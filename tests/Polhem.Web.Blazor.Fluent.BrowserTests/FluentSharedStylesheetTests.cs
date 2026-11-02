using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared stylesheet test on the Fluent UI UI package.
    /// </summary>
    public class FluentSharedStylesheetTests : SharedStylesheetTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentSharedStylesheetTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentSharedStylesheetTests(FluentSiteFixture fixture) : base(fixture)
        {
        }
    }
}
