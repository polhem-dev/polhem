namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// The test site with a list long enough to scroll.
    /// </summary>
    public sealed class MudCardListFixture : MudSiteFixture
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MudCardListFixture"/>.
        /// </summary>
        public MudCardListFixture()
        {
            Backend.TotalRows = 300;
            Backend.DistinctRows = true;
        }
    }
}
