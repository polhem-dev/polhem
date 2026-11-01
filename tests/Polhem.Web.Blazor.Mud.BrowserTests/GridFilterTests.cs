using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Definition.Filters;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// What a column filter does only in a real browser: the popover, the keyboard, and closing on a click outside.
    /// </summary>
    public class GridFilterTests : IClassFixture<GridBrowserFixture>
    {
        private const string Button = ".polhem-grid-filter__button";
        private readonly GridBrowserFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="GridFilterTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public GridFilterTests(GridBrowserFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenFilterAsync()
        {
            var page = await _fixture.Browser.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(_fixture.Address + "/form").ConfigureAwait(false);
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached }).ConfigureAwait(false);
            await page.Locator(Button).First.ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(".polhem-grid-filter__first input")).ToBeFocusedAsync().ConfigureAwait(false);
            // MudBlazor keeps a popover off screen until its script has placed it, so focus alone does not show it.
            await Assertions.Expect(page.Locator(".polhem-grid-filter__form")).ToBeInViewportAsync().ConfigureAwait(false);
            return page;
        }

        [BrowserFact]
        [DisplayName("Typing and pressing Enter applies the filter, marks the column and closes the panel")]
        public async Task TypeThenEnter_Applies()
        {
            var page = await OpenFilterAsync();

            await page.Keyboard.TypeAsync("Ali");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator(".polhem-grid-filter__form")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator(".polhem-grid-filter__button--active")).ToHaveCountAsync(1);
            Assert.Equal("Ali", Assert.IsType<FilterCondition>(_fixture.Backend.LastFilter).Value?.ToString());
        }

        [BrowserFact]
        [DisplayName("A click outside closes the panel and drops what was typed")]
        public async Task ClickOutside_Drops()
        {
            var page = await OpenFilterAsync();
            int reads = _fixture.Backend.GetListCount;

            await page.Keyboard.TypeAsync("Ali");
            await page.Mouse.ClickAsync(700, 500);

            await Assertions.Expect(page.Locator(".polhem-grid-filter__form")).ToHaveCountAsync(0);
            Assert.Equal(reads, _fixture.Backend.GetListCount);
            await Assertions.Expect(page.Locator(".polhem-grid-filter__button--active")).ToHaveCountAsync(0);
        }

        [BrowserFact]
        [DisplayName("Escape closes the panel and drops what was typed")]
        public async Task Escape_Drops()
        {
            var page = await OpenFilterAsync();
            int reads = _fixture.Backend.GetListCount;

            await page.Keyboard.TypeAsync("Ali");
            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(".polhem-grid-filter__form")).ToHaveCountAsync(0);
            Assert.Equal(reads, _fixture.Backend.GetListCount);
        }

        [BrowserFact]
        [DisplayName("Clicking the filter button does not sort or select a row")]
        public async Task FilterButton_DoesNotSort()
        {
            var page = await OpenFilterAsync();

            Assert.Null(_fixture.Backend.LastSortField);
            await Assertions.Expect(page.Locator(".polhem-grid-filter__form")).ToBeVisibleAsync();
        }
    }
}
