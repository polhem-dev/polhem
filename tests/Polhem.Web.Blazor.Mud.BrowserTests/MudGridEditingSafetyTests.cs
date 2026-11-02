using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared grid editing tests on the MudBlazor UI package.
    /// </summary>
    public class MudGridEditingSafetyTests : GridEditingSafetyTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudGridEditingSafetyTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudGridEditingSafetyTests(MudSiteFixture fixture) : base(fixture)
        {
        }
    }
}
