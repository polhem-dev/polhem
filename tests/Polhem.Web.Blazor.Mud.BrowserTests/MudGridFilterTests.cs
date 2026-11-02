using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared column filter tests on the MudBlazor UI package.
    /// </summary>
    public class MudGridFilterTests : GridFilterTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudGridFilterTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudGridFilterTests(MudSiteFixture fixture) : base(fixture)
        {
        }
    }
}
