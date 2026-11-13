using Polhem.Web.Blazor.BrowserTests;
using Xunit.Abstractions;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared card list tests on the Fluent UI UI package.
    /// </summary>
    public class FluentCardListTests : CardListTests, IClassFixture<FluentCardListFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentCardListTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="output">Where the measurements go.</param>
        public FluentCardListTests(FluentCardListFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}
