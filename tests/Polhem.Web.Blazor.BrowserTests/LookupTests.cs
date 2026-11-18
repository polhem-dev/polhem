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
    /// <see cref="LookupTestData.LookupField"/> in a form scope and shows the field's value in <c>#data</c>, and a page at
    /// <c>/row-edit-lookup</c> that shows its row edit dialog over the line of
    /// <see cref="LookupTestData.BuildOrderWithLookupLine"/>, on a site whose backend is the <see cref="FakeLookupSource"/>
    /// it passes in.
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

        /// <summary>Gets the selector of the open row edit dialog.</summary>
        protected abstract string RowEditDialog { get; }

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

        [BrowserTheory]
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

        [BrowserTheory]
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

        [BrowserTheory]
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

        [BrowserTheory]
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

        [BrowserTheory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("Escape closes a lookup opened from the row edit dialog, and the row edit dialog stays open with the focus")]
        public async Task EscapeInRowEdit_ClosesOnlyTheLookup(bool slow)
        {
            var page = await _fixture.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 390, Height = 800 } });
            await page.GotoAsync((slow ? _fixture.SlowAddress : _fixture.Address) + "/row-edit-lookup");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            var panel = page.Locator($"{RowEditDialog} .polhem-row-edit-panel");
            await Assertions.Expect(panel).ToBeVisibleAsync();
            await panel.Locator(LookupButton).ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} {Row}")).ToHaveCountAsync(2);
            await page.WaitForFunctionAsync("selector => document.activeElement?.closest(selector) != null", Dialog);

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await Assertions.Expect(panel).ToBeVisibleAsync();
            await page.WaitForFunctionAsync("selector => document.activeElement?.closest(selector) != null", RowEditDialog);

            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(500);
            await Assertions.Expect(panel).ToBeVisibleAsync();
        }

        /// <summary>Opens the row edit page at phone width.</summary>
        /// <returns>The page, and the row edit panel once it shows.</returns>
        protected async Task<(IPage Page, ILocator Panel)> OpenRowEditAsync()
        {
            var page = await _fixture.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 390, Height = 800 } });
            await page.GotoAsync(_fixture.Address + "/row-edit-lookup");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            var panel = page.Locator($"{RowEditDialog} .polhem-row-edit-panel");
            await Assertions.Expect(panel).ToBeVisibleAsync();
            return (page, panel);
        }

        /// <summary>
        /// Moves the focus to the body, where it falls back when a render removes the focused element.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The operation.</returns>
        protected static async Task BlurAsync(IPage page)
        {
            await page.EvaluateAsync("() => document.activeElement?.blur()");
            Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement === document.body"));
        }

        [BrowserFact]
        [DisplayName("Escape with the focus on the body leaves the row edit dialog open")]
        public async Task EscapeOnBody_KeepsRowEdit()
        {
            var (page, panel) = await OpenRowEditAsync();
            await BlurAsync(page);

            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(500);

            await Assertions.Expect(panel).ToBeVisibleAsync();
            await panel.Locator(LookupButton).ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} {Row}")).ToHaveCountAsync(2);
        }

        [BrowserFact]
        [DisplayName("Escape with the focus on the body closes a lookup opened from the row edit dialog and nothing else")]
        public async Task EscapeOnBodyInRowEdit_ClosesOnlyTheLookup()
        {
            var (page, panel) = await OpenRowEditAsync();
            await panel.Locator(LookupButton).ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} {Row}")).ToHaveCountAsync(2);
            await page.WaitForFunctionAsync("selector => document.activeElement?.closest(selector) != null", Dialog);
            await BlurAsync(page);

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await page.WaitForTimeoutAsync(500);
            await Assertions.Expect(panel).ToBeVisibleAsync();
        }

        [BrowserFact]
        [DisplayName("Escape with the focus on the body closes the lookup and writes nothing")]
        public async Task EscapeOnBody_Closes()
        {
            var page = await OpenAsync(false);
            await BlurAsync(page);

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
