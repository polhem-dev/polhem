using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared grid editing tests on the Fluent UI UI package.
    /// </summary>
    public class FluentGridEditingSafetyTests : GridEditingSafetyTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridEditingSafetyTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentGridEditingSafetyTests(FluentSiteFixture fixture) : base(fixture)
        {
        }
    }
}
