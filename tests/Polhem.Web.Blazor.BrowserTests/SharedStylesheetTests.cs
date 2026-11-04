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

        // A web component keeps its input in a shadow root, where the shared rule does not reach, so the input that has the
        // focus is followed into it.
        [BrowserFact]
        [DisplayName("A numeric editor shows its value right-aligned")]
        public async Task NumericEditor_IsRightAligned()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address);
            var qty = page.Locator("tr.polhem-grid__row").First.Locator(".polhem-grid-cell__text").Nth(1);
            await Assertions.Expect(qty).ToBeVisibleAsync();
            // A cell opens its editor only once the circuit connected, which is when the editor module loads.
            await page.WaitForFunctionAsync(
                "performance.getEntriesByType('resource').some(e => e.name.includes('polhem-grid-editor.js'))");

            await qty.FocusAsync();
            await Assertions.Expect(page.Locator(".polhem-field-editor--numeric")).ToBeVisibleAsync();
            var alignment = await page.EvaluateAsync<string>("""
                () => {
                    let element = document.activeElement;
                    while (element && element.shadowRoot && element.shadowRoot.activeElement) element = element.shadowRoot.activeElement;
                    return element && element.tagName === 'INPUT' ? getComputedStyle(element).textAlign : 'no input';
                }
                """);

            Assert.Equal("right", alignment);
        }
    }
}
