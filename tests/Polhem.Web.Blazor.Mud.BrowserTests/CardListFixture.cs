namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// The test site with a list long enough to scroll.
    /// </summary>
    public sealed class CardListFixture : GridBrowserFixture
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CardListFixture"/>.
        /// </summary>
        public CardListFixture()
        {
            Backend.TotalRows = 300;
            Backend.DistinctRows = true;
        }
    }
}
