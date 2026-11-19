using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Definition.Filters;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// What the query pane does only in a real browser: where the focus goes, Enter and Escape, a slow link, and the
    /// pane over the page on a phone.
    /// </summary>
    public abstract class QueryPaneTests
    {
        private const string QueryButton = ".polhem-form-page__query";
        private const string Pane = ".polhem-criteria-panel";
        private const string AddButton = ".polhem-criteria-panel__add button";
        private const string ValueInput = ".polhem-criteria-row__value input";
        private const string Chip = ".polhem-criteria-chip__open";
        private readonly BrowserSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="QueryPaneTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        protected QueryPaneTests(BrowserSiteFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenPaneAsync(string address, int width = 1280)
        {
            var page = await _fixture.Browser.NewPageAsync().ConfigureAwait(false);
            await page.SetViewportSizeAsync(width, 800).ConfigureAwait(false);
            await page.GotoAsync(address + "/form").ConfigureAwait(false);
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached }).ConfigureAwait(false);
            await page.Locator(QueryButton).ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(Pane)).ToBeVisibleAsync().ConfigureAwait(false);
            return page;
        }

        // Adds a condition on the Name field; the new row's value takes the focus.
        private static async Task AddNameAsync(IPage page)
        {
            await page.Locator(AddButton).ClickAsync().ConfigureAwait(false);
            await page.Locator(".polhem-criteria-panel__field", new PageLocatorOptions { HasText = "Name" }).ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator(ValueInput)).ToBeFocusedAsync().ConfigureAwait(false);
        }

        private string? FilterText() => (_fixture.Backend.LastFilter as FilterCondition)?.Value?.ToString();

        [BrowserFact]
        [DisplayName("The pane opens with the focus on add condition, and an added row takes the focus")]
        public async Task Open_FocusesAddThenNewRow()
        {
            var page = await OpenPaneAsync(_fixture.Address);

            await Assertions.Expect(page.Locator(AddButton)).ToBeFocusedAsync();
            await AddNameAsync(page);
        }

        [BrowserFact]
        [DisplayName("Typing a value and pressing Enter runs the query and shows its chip")]
        public async Task TypeThenEnter_RunsQuery()
        {
            var page = await OpenPaneAsync(_fixture.Address);
            await AddNameAsync(page);

            await page.Keyboard.TypeAsync("Ali");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator(Chip)).ToHaveCountAsync(1);
            Assert.Equal("Ali", FilterText());
        }

        [BrowserFact]
        [DisplayName("Escape in a value closes the pane, and opening it again focuses that row")]
        public async Task Escape_ClosesPane()
        {
            var page = await OpenPaneAsync(_fixture.Address);
            await AddNameAsync(page);

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Pane)).ToHaveCountAsync(0);
            await page.Locator(QueryButton).ClickAsync();
            await Assertions.Expect(page.Locator(ValueInput)).ToBeFocusedAsync();
        }

        [BrowserFact]
        [DisplayName("Days entered into a date range stay in the boxes and filter the list")]
        public async Task DateRange_Entered_Filters()
        {
            var page = await OpenPaneAsync(_fixture.Address);
            await page.Locator(AddButton).ClickAsync();
            await page.Locator(".polhem-criteria-panel__field", new PageLocatorOptions { HasText = "Hired" }).ClickAsync();
            var from = page.Locator(".polhem-criteria-row__from input");
            var to = page.Locator(".polhem-criteria-row__to input");
            await Assertions.Expect(from).ToBeFocusedAsync();

            // The order of a date input's parts follows the operating system's format, which differs between
            // machines, so the days are filled in whole. A date input reports no value until every part is entered,
            // so a part typed alone sends nothing that could rewrite the box.
            await from.FillAsync("2026-03-01");
            await to.FillAsync("2026-03-31");
            await to.FocusAsync();

            await Assertions.Expect(from).ToHaveValueAsync("2026-03-01");
            await Assertions.Expect(to).ToHaveValueAsync("2026-03-31");
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.Locator(Chip)).ToHaveCountAsync(1);
            var conditions = Assert.IsType<FilterGroup>(_fixture.Backend.LastFilter).Nodes!.Cast<FilterCondition>().ToList();
            Assert.Equal(new DateOnly(2026, 3, 1), conditions[0].Value);
            Assert.Equal(new DateOnly(2026, 3, 31), conditions[1].Value);
        }

        [BrowserFact]
        [DisplayName("Over a slow link, Enter pressed right after typing runs the query with the whole value")]
        public async Task SlowLink_EnterRightAfterTyping_UsesWholeValue()
        {
            var page = await OpenPaneAsync(_fixture.SlowAddress);
            await AddNameAsync(page);

            await page.Keyboard.TypeAsync("Alice");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator(Chip)).ToHaveCountAsync(1);
            Assert.Equal("Alice", FilterText());
        }

        [BrowserFact]
        [DisplayName("Save as opens with the focus in the name box, and Enter saves the conditions under that name")]
        public async Task SaveAs_FocusesNameAndEnterSaves()
        {
            var page = await OpenPaneAsync(_fixture.Address);
            await AddNameAsync(page);
            await page.Keyboard.TypeAsync("Ali");
            int saves = _fixture.Backend.UserQuerySaves.Count;

            await page.Locator(".polhem-user-query-bar__actions button").ClickAsync();
            await page.Locator(".polhem-user-query-bar__save-as").ClickAsync();
            await Assertions.Expect(page.Locator(".polhem-name-prompt-dialog__name input")).ToBeFocusedAsync();
            await page.Keyboard.TypeAsync("Mine");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator(".polhem-name-prompt-dialog__name")).ToHaveCountAsync(0);
            Assert.Equal(saves + 1, _fixture.Backend.UserQuerySaves.Count);
            Assert.Equal("Mine", _fixture.Backend.UserQuerySaves[^1].Name);
        }

        [BrowserTheory]
        [InlineData(1400, false)]
        [InlineData(900, true)]
        [DisplayName("The pane sits beside a wide list, and lies over the list when beside it the list would be too narrow")]
        public async Task Width_DecidesBesideOrOver(int width, bool over)
        {
            var page = await OpenPaneAsync(_fixture.Address, width);

            var layout = await page.Locator(".polhem-query-layout").BoundingBoxAsync();
            var list = await page.Locator(".polhem-query-layout__list").BoundingBoxAsync();
            var pane = await page.Locator(".polhem-query-pane").BoundingBoxAsync();
            Assert.NotNull(layout);
            Assert.NotNull(list);
            Assert.NotNull(pane);
            Assert.Equal(over, list.Width >= layout.Width - 1);
            Assert.Equal(over, pane.X < list.X + list.Width);
        }

        [BrowserFact]
        [DisplayName("On a phone the pane covers the page, and Enter closes it to show the cards")]
        public async Task Phone_PaneCoversPage()
        {
            var page = await OpenPaneAsync(_fixture.Address, width: 400);
            var box = await page.Locator(".polhem-query-pane").BoundingBoxAsync();
            Assert.NotNull(box);
            Assert.True(box.Width >= 390, $"The pane is {box.Width}px wide.");
            await AddNameAsync(page);

            await page.Keyboard.TypeAsync("Ali");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator(Pane)).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator(Chip)).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator(".polhem-card").First).ToBeVisibleAsync();
        }
    }
}
