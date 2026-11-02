using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared shell tests on the MudBlazor UI package.
    /// </summary>
    public class MudShellTests : ShellTests, IClassFixture<MudShellFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudShellTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudShellTests(MudShellFixture fixture) : base(fixture, fixture.ShellBackend)
        {
        }
    }
}
