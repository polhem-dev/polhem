using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BehaviorTests;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// What a lookup does only in a real browser, in a form scope and over a slow link: a key pressed before the dialog
    /// arrived, a search typed and submitted at once, a double-click on a row.
    /// </summary>
    /// <remarks>
    /// A UI package derives from it over a test page at <c>/lookup</c> that places a <c>ButtonEdit</c> for
    /// <see cref="LookupTestData.LookupField"/> in a form scope and shows the field's value in <c>#data</c>, on a site whose
    /// backend is the <see cref="FakeLookupSource"/> it passes in.
    /// </remarks>
    public abstract class LookupTests
    {
        private const string Row = "tr.polhem-grid__row";
        private readonly BrowserSiteFixture _fixture;
        private readonly FakeLookupSource _source;

        /// <summary>
        /// Initializes a new instance of <see cref="LookupTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="source">The lookup target the site's backend serves.</param>
        protected LookupTests(BrowserSiteFixture fixture, FakeLookupSource source)
        {
            _fixture = fixture;
            _source = source;
        }

        /// <summary>Gets the selector of the open lookup dialog.</summary>
        protected abstract string Dialog { get; }

        /// <summary>Gets the selector of the button that opens the lookup.</summary>
        protected abstract string LookupButton { get; }

        private async Task<IPage> OpenAsync(bool slow)
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync((slow ? _fixture.SlowAddress : _fixture.Address) + "/lookup");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.Locator(LookupButton).ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} {Row}")).ToHaveCountAsync(2);
            // The rows are in the markup before the dialog takes the focus, and a key means nothing until it has.
            await page.WaitForFunctionAsync("selector => document.activeElement?.closest(selector) != null", Dialog);
            return page;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Choosing a row and OK writes the value and closes the lookup")]
        public async Task SelectThenOk_Writes(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Locator($"{Dialog} {Row}").Nth(1).ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} tr.polhem-grid__row--selected")).ToHaveCountAsync(1);
            await page.Locator(".polhem-lookup-panel__ok").ClickAsync();

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#data")).ToContainTextAsync(LookupTestData.SalesId.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("A double-click on a row writes the value and closes the lookup")]
        public async Task DoubleClick_Writes(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Locator($"{Dialog} {Row}").First.DblClickAsync();

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#data")).ToContainTextAsync(LookupTestData.EngineeringId.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Typing a search and pressing Enter at once searches for what was typed")]
        public async Task TypeThenEnter_Searches(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Locator($"{Dialog} .polhem-lookup-panel__search-input input").First.ClickAsync();
            await page.Keyboard.TypeAsync("Sal");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator($"{Dialog} {Row}")).ToHaveCountAsync(1);
            Assert.Equal("Sal", _source.Connector.Searches[^1]);
            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Escape closes the lookup and writes nothing")]
        public async Task Escape_Closes(bool slow)
        {
            var page = await OpenAsync(slow);

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#data")).ToHaveTextAsync(Guid.Empty.ToString());
        }

        [BrowserFact]
        [DisplayName("A click outside closes the lookup and writes nothing")]
        public async Task Backdrop_Closes()
        {
            var page = await OpenAsync(false);

            await page.Mouse.ClickAsync(5, 5);

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("#data")).ToHaveTextAsync(Guid.Empty.ToString());
        }
    }
}
