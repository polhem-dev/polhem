using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// What the Fluent stylesheet draws, which only a browser computes: a read-only input keeps only its bottom line, and
    /// a column the layout gives no width shares the grid's spare width instead of leaving it empty.
    /// </summary>
    public class FluentPresentationTests : IClassFixture<FluentSiteFixture>
    {
        private readonly FluentSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="FluentPresentationTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentPresentationTests(FluentSiteFixture fixture) => _fixture = fixture;

        [BrowserFact]
        [DisplayName("A read-only text input keeps only its bottom line and an editable one keeps its frame")]
        public async Task ReadOnlyInput_KeepsOnlyTheBottomLine()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address);
            await page.WaitForFunctionAsync("customElements.get('fluent-text-input') !== undefined");

            var frames = await page.EvaluateAsync<string[]>("""
                async () => {
                    const add = readOnly => {
                        const input = document.createElement('fluent-text-input');
                        if (readOnly) input.setAttribute('readonly', '');
                        document.body.appendChild(input);
                        return input;
                    };
                    const inputs = [add(true), add(false)];
                    await new Promise(requestAnimationFrame);
                    return inputs.map(input => {
                        const root = getComputedStyle(input.shadowRoot.querySelector('[part=root]'));
                        return root.borderTopColor + '|' + root.borderBottomColor;
                    });
                }
                """);

            Assert.StartsWith("rgba(0, 0, 0, 0)|", frames[0], StringComparison.Ordinal);
            Assert.DoesNotContain("rgba(0, 0, 0, 0)|rgba(0, 0, 0, 0)", frames[0], StringComparison.Ordinal);
            Assert.DoesNotMatch("^rgba\\(0, 0, 0, 0\\)\\|", frames[1]);
        }

        [BrowserFact]
        [DisplayName("Columns the layout gives no width share the grid's whole width")]
        public async Task ColumnsWithoutWidth_FillTheGrid()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.SetViewportSizeAsync(1200, 800);
            await page.GotoAsync(_fixture.Address);
            await Assertions.Expect(page.Locator("tr.polhem-grid__row").First).ToBeVisibleAsync();

            var widths = await page.EvaluateAsync<double[]>("""
                () => {
                    const table = document.querySelector('.polhem-grid table');
                    const headers = [...table.querySelectorAll('th[cell-type=columnheader]')];
                    return [table.getBoundingClientRect().width, ...headers.map(h => h.getBoundingClientRect().width)];
                }
                """);

            var columns = widths.Skip(1).ToArray();
            Assert.All(columns, width => Assert.True(width > 120, $"A column is {width}px wide."));
            Assert.InRange(columns.Sum(), widths[0] - 2, widths[0] + 2);
        }
    }
}
