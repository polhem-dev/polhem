using Polhem.Web.Blazor.BrowserTests;
using Xunit.Abstractions;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared first load test on the Fluent UI UI package.
    /// </summary>
    public class FluentGridPerformanceTests : GridPerformanceTests, IClassFixture<FluentSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridPerformanceTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="output">Where the measurements go.</param>
        public FluentGridPerformanceTests(FluentSiteFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }

        /// <inheritdoc />
        protected override long FirstLoadLimitBytes => 2_100_000;
    }
}
