using System.ComponentModel;
using Microsoft.Playwright;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// The layout rules live in the shared package and reach a page through the UI package's stylesheet, so a wrong path
    /// breaks only the look. No build or bUnit test sees that.
    /// </summary>
    public abstract class SharedStylesheetTests
    {
        private readonly BrowserSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="SharedStylesheetTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        protected SharedStylesheetTests(BrowserSiteFixture fixture) => _fixture = fixture;

        [BrowserFact]
        [DisplayName("The layout rules of the shared package apply on a page that links only the UI package's stylesheet")]
        public async Task SharedLayoutRules_Apply()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address);

            await Assertions.Expect(page.Locator(".polhem-grid__toolbar")).ToHaveCSSAsync("display", "flex");
        }
    }
}
