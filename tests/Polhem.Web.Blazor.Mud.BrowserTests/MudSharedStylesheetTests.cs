using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared stylesheet test on the MudBlazor UI package.
    /// </summary>
    public class MudSharedStylesheetTests : SharedStylesheetTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudSharedStylesheetTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudSharedStylesheetTests(MudSiteFixture fixture) : base(fixture)
        {
        }
    }
}
