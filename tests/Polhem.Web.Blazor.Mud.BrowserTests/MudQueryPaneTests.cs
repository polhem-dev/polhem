using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared query pane browser tests on the MudBlazor UI package.
    /// </summary>
    public class MudQueryPaneTests : QueryPaneTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudQueryPaneTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudQueryPaneTests(MudSiteFixture fixture) : base(fixture)
        {
        }
    }
}
