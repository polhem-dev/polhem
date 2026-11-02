namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// The test site with a list long enough to scroll.
    /// </summary>
    public sealed class FluentCardListFixture : FluentSiteFixture
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentCardListFixture"/>.
        /// </summary>
        public FluentCardListFixture()
        {
            Backend.TotalRows = 300;
            Backend.DistinctRows = true;
        }
    }
}
