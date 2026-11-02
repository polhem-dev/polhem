using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared shell tests on the Fluent UI UI package.
    /// </summary>
    public class FluentShellTests : ShellTests, IClassFixture<FluentShellFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentShellTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentShellTests(FluentShellFixture fixture) : base(fixture, fixture.ShellBackend)
        {
        }
    }
}
