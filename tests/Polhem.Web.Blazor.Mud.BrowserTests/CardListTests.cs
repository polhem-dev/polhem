using System.ComponentModel;
using Microsoft.Playwright;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// What the phone card list does only in a real browser: reading the next page as it scrolls, and keeping the
    /// scroll position while it does.
    /// </summary>
    public class CardListTests : IClassFixture<CardListFixture>
    {
        private const string Card = ".polhem-card";
        private readonly CardListFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="CardListTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public CardListTests(CardListFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenAsync(bool slow = false, int width = 400)
        {
            var page = await _fixture.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 700 } }).ConfigureAwait(false);
            await page.GotoAsync((slow ? _fixture.SlowAddress : _fixture.Address) + "/form").ConfigureAwait(false);
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached }).ConfigureAwait(false);
            return page;
        }

        private static Task ScrollToEndAsync(IPage page) => page.EvaluateAsync("window.scrollTo(0, document.documentElement.scrollHeight)");

        [BrowserFact]
        [DisplayName("A narrow screen shows cards, and scrolling to the end reads the next page")]
        public async Task Scroll_ReadsNextPage()
        {
            var page = await OpenAsync();
            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(50);

            await ScrollToEndAsync(page);

            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(100);
        }

        [BrowserFact]
        [DisplayName("A page arriving does not move what is on screen")]
        public async Task Scroll_KeepsPosition()
        {
            var page = await OpenAsync();
            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(50);
            await ScrollToEndAsync(page);
            var before = await page.EvaluateAsync<double>("window.scrollY");

            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(100);

            Assert.Equal(before, await page.EvaluateAsync<double>("window.scrollY"));
        }

        [BrowserFact]
        [DisplayName("Scrolling on over a slow link reads each page once and reaches the last row")]
        public async Task SlowLink_ScrollOn_ReadsEachPageOnce()
        {
            var page = await OpenAsync(slow: true);
            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(50);
            int start = _fixture.Backend.ListPaging.Count;

            // The next page starts before the end is reached, so a scroll can read more than one page.
            while (await page.Locator(Card).CountAsync() < 300)
            {
                await ScrollToEndAsync(page);
                await page.WaitForTimeoutAsync(100);
            }

            Assert.Equal([2, 3, 4, 5, 6], _fixture.Backend.ListPaging.Skip(start).Select(p => p.Page));
            await Assertions.Expect(page.Locator(".polhem-card-list__end")).ToHaveCountAsync(0);
        }

        [BrowserFact]
        [DisplayName("Crossing to a wide screen shows the grid from the first page, and back shows cards")]
        public async Task Resize_SwitchesSurface()
        {
            var page = await OpenAsync();
            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(50);
            await ScrollToEndAsync(page);
            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(100);

            await page.SetViewportSizeAsync(1000, 700);
            await Assertions.Expect(page.Locator("tr.polhem-grid__row")).ToHaveCountAsync(50);
            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(0);

            await page.SetViewportSizeAsync(400, 700);
            await Assertions.Expect(page.Locator(Card)).ToHaveCountAsync(50);
        }
    }
}
