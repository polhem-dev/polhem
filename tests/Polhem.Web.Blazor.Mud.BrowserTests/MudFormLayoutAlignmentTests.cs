using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared form layout alignment tests on the MudBlazor UI package.
    /// </summary>
    public class MudFormLayoutAlignmentTests : FormLayoutAlignmentTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudFormLayoutAlignmentTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudFormLayoutAlignmentTests(MudSiteFixture fixture) : base(fixture)
        {
        }
    }
}
