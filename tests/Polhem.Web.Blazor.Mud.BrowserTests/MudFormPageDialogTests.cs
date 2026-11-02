using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs the shared form page dialog tests on the MudBlazor UI package.
    /// </summary>
    public class MudFormPageDialogTests : FormPageDialogTests, IClassFixture<MudSiteFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudFormPageDialogTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudFormPageDialogTests(MudSiteFixture fixture) : base(fixture)
        {
        }

        /// <inheritdoc />
        protected override string Dialog => ".mud-dialog";
    }
}
