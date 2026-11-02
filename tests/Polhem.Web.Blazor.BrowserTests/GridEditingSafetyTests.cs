using System.ComponentModel;
using Microsoft.Playwright;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// A cell opens its editor after a round trip, and a key typed in between must not be lost or land elsewhere. Only a
    /// real browser shows this: replaying a key from script, and whether leaving then fires <c>change</c>, are browser
    /// behavior that bUnit does not run.
    /// </summary>
    public abstract class GridEditingSafetyTests
    {
        private const string Initial = "Bolt|1;Nut|2";
        private readonly BrowserSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="GridEditingSafetyTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        protected GridEditingSafetyTests(BrowserSiteFixture fixture) => _fixture = fixture;

        private static ILocator Cell(IPage page, int row, int column)
            => page.Locator("tr.polhem-grid__row").Nth(row).Locator(".polhem-grid-cell__text, .polhem-grid-cell__editor").Nth(column);

        private static Task WaitForDataAsync(IPage page, string expected)
            => Assertions.Expect(page.Locator("#data")).ToHaveTextAsync(expected);

        private async Task<IPage> OpenAsync(bool slow = false)
        {
            var page = await _fixture.Browser.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(slow ? _fixture.SlowAddress : _fixture.Address).ConfigureAwait(false);
            await Assertions.Expect(Cell(page, 0, 0)).ToHaveTextAsync("Bolt").ConfigureAwait(false);
            // The module holding keys loads with the first cell, after the circuit connects.
            await page.WaitForFunctionAsync(
                "performance.getEntriesByType('resource').some(e => e.name.includes('polhem-grid-editor.js'))").ConfigureAwait(false);
            return page;
        }

        [BrowserFact]
        [DisplayName("Text typed before the editor arrives is written when the user tabs away before it arrives")]
        public async Task SlowLink_TypeThenTabBeforeEditor_WritesTheText()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).FocusAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Keyboard.PressAsync("Tab");

            await WaitForDataAsync(page, "Washer|1;Nut|2");
        }

        [BrowserFact]
        [DisplayName("After the tab that waited for the editor, the next cell has the editor and the focus")]
        public async Task SlowLink_TypeThenTabBeforeEditor_MovesToTheNextCell()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).FocusAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Keyboard.PressAsync("Tab");

            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor input")).ToBeFocusedAsync();
            await Assertions.Expect(page.Locator("tr.polhem-grid__row").Nth(0).Locator(".polhem-field-editor--numeric input")).ToHaveCountAsync(1);
        }

        [BrowserFact]
        [DisplayName("A click on another cell before the editor arrives does not move the text there")]
        public async Task SlowLink_TypeThenClickOtherCell_KeepsTextInItsCell()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).FocusAsync();
            await page.Keyboard.TypeAsync("Washer");
            await Cell(page, 1, 0).ClickAsync();
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor input")).ToHaveValueAsync("Washer");
            await page.Locator("#outside").FocusAsync();

            await WaitForDataAsync(page, "Washer|1;Nut|2");
        }

        [BrowserFact]
        [DisplayName("Text typed before the editor arrives is written when the user clicks outside the grid before it arrives")]
        public async Task SlowLink_TypeThenClickOutside_WritesTheText()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).ClickAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Locator("#outside").ClickAsync();

            await WaitForDataAsync(page, "Washer|1;Nut|2");
            await Assertions.Expect(page.Locator("#outside")).ToBeFocusedAsync();
        }

        [BrowserFact]
        [DisplayName("A button clicked before the editor arrives runs after the typed text is written")]
        public async Task SlowLink_TypeThenClickButton_RunsAfterTheTextIsWritten()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).ClickAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Locator("#capture").ClickAsync();

            await Assertions.Expect(page.Locator("#captured")).ToHaveTextAsync("Washer|1;Nut|2");
        }

        [BrowserFact]
        [DisplayName("A second click on the same cell before the editor arrives stays in the cell")]
        public async Task SlowLink_TypeThenClickSameCell_StaysInTheEditor()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).ClickAsync();
            await page.Keyboard.TypeAsync("Washer");
            await Cell(page, 0, 0).ClickAsync();

            var input = page.Locator(".polhem-grid-cell__editor input");
            await Assertions.Expect(input).ToHaveValueAsync("Washer");
            await Assertions.Expect(input).ToBeFocusedAsync();
            await WaitForDataAsync(page, Initial);
        }

        [BrowserFact]
        [DisplayName("Keys typed after a click outside the grid go to where the click landed")]
        public async Task SlowLink_TypeClickOutsideThenType_SplitsTheText()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).ClickAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Locator("#outside").ClickAsync();
            await page.Keyboard.TypeAsync("note");

            await WaitForDataAsync(page, "Washer|1;Nut|2");
            await Assertions.Expect(page.Locator("#outside")).ToHaveValueAsync("note");
        }

        [BrowserFact]
        [DisplayName("Escape after a click outside the grid drops the held keys")]
        public async Task SlowLink_TypeClickOutsideThenEscape_DropsTheKeys()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).ClickAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Locator("#outside").ClickAsync();
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor input")).ToHaveValueAsync("Bolt");
            await page.Locator("#outside").FocusAsync();

            await WaitForDataAsync(page, Initial);
        }

        [BrowserFact]
        [DisplayName("Replayed text is committed when the user leaves the editor after it arrives")]
        public async Task ReplayedText_LeavingTheEditor_Commits()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).FocusAsync();
            await page.Keyboard.TypeAsync("Washer");
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor input")).ToHaveValueAsync("Washer");
            await page.Locator("#outside").FocusAsync();

            await WaitForDataAsync(page, "Washer|1;Nut|2");
        }

        [BrowserFact]
        [DisplayName("A number typed before the editor arrives is written as a number")]
        public async Task SlowLink_NumberTyped_WritesTheNumber()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 1).FocusAsync();
            await page.Keyboard.TypeAsync("12.5");
            await page.Keyboard.PressAsync("Tab");

            await WaitForDataAsync(page, "Bolt|12.5;Nut|2");
        }

        [BrowserFact]
        [DisplayName("Escape in an open editor drops what was typed and returns the cell to text with the focus")]
        public async Task Escape_InEditor_DropsTheText()
        {
            var page = await OpenAsync();

            await Cell(page, 0, 0).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor input").WaitForAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor")).ToHaveCountAsync(0);
            await Assertions.Expect(Cell(page, 0, 0)).ToBeFocusedAsync();
            await page.Locator("#outside").FocusAsync();
            await WaitForDataAsync(page, Initial);
        }

        [BrowserFact]
        [DisplayName("Escape before the editor arrives drops the held keys")]
        public async Task SlowLink_EscapeBeforeEditor_DropsTheKeys()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).FocusAsync();
            await page.Keyboard.TypeAsync("Wa");
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor input")).ToHaveValueAsync("Bolt");
            await page.Locator("#outside").FocusAsync();

            await WaitForDataAsync(page, Initial);
        }

        [BrowserFact]
        [DisplayName("Tabbing through the cells keeps one editor open")]
        public async Task Tab_ThroughCells_KeepsOneEditor()
        {
            var page = await OpenAsync();

            await Cell(page, 0, 0).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor input").WaitForAsync();
            foreach (var _ in Enumerable.Range(0, 3))
                await page.Keyboard.PressAsync("Tab");

            await Assertions.Expect(page.Locator("tr.polhem-grid__row").Nth(1).Locator(".polhem-field-editor--numeric input")).ToBeFocusedAsync();
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor")).ToHaveCountAsync(1);
            await WaitForDataAsync(page, Initial);
        }

        [BrowserFact]
        [DisplayName("Tabbing faster than the editors arrive still ends on the right cell")]
        public async Task SlowLink_FastTabs_EndOnTheRightCell()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor input").WaitForAsync();
            foreach (var _ in Enumerable.Range(0, 3))
                await page.Keyboard.PressAsync("Tab");

            await Assertions.Expect(page.Locator("tr.polhem-grid__row").Nth(1).Locator(".polhem-field-editor--numeric input")).ToBeFocusedAsync();
            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor")).ToHaveCountAsync(1);
            await WaitForDataAsync(page, Initial);
        }

        [BrowserFact]
        [DisplayName("Text typed after fast tabs lands in the cell the tabs reached")]
        public async Task SlowLink_FastTabsThenType_WritesTheCellReached()
        {
            var page = await OpenAsync(slow: true);

            await Cell(page, 0, 0).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor input").WaitForAsync();
            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.TypeAsync("Washer");
            await page.Keyboard.PressAsync("Tab");

            await WaitForDataAsync(page, "Bolt|1;Washer|2");
        }

        [BrowserFact]
        [DisplayName("Entering View mode closes the open editor")]
        public async Task ViewMode_ClosesTheEditor()
        {
            var page = await OpenAsync();

            await Cell(page, 0, 0).ClickAsync();
            await page.Locator(".polhem-grid-cell__editor input").WaitForAsync();
            await page.Locator("#mode").ClickAsync();

            await Assertions.Expect(page.Locator(".polhem-grid-cell__editor")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator(".polhem-grid-cell__text")).ToHaveCountAsync(0);
        }
    }
}
