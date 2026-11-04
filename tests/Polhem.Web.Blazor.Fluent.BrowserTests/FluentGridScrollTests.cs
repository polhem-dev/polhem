using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// The Fluent grid scrolls sideways when its columns are wider than the page. Its height comes out fractional, so a
    /// vertical overflow of <c>auto</c> drew a scrollbar for half a pixel over rows that all fit.
    /// </summary>
    public class FluentGridScrollTests : IClassFixture<FluentSiteFixture>
    {
        private readonly FluentSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="FluentGridScrollTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentGridScrollTests(FluentSiteFixture fixture) => _fixture = fixture;

        [BrowserFact]
        [DisplayName("A grid scrolls sideways and never shows a vertical scrollbar")]
        public async Task Grid_ScrollsOnlySideways()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address);
            var grid = page.Locator(".polhem-grid").First;

            await Assertions.Expect(grid).ToHaveCSSAsync("overflow-x", "auto");
            await Assertions.Expect(grid).ToHaveCSSAsync("overflow-y", "hidden");
        }
    }
}
