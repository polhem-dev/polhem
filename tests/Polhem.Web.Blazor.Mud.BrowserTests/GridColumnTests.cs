using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// What resizing and moving a column does only in a real browser: native dragging, and what editing does afterwards.
    /// </summary>
    public class GridColumnTests : IClassFixture<GridBrowserFixture>
    {
        private const string Headers = ".polhem-grid thead th";
        private const string FirstRow = ".polhem-grid tbody tr";
        private readonly GridBrowserFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="GridColumnTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public GridColumnTests(GridBrowserFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenAsync(string address)
        {
            var page = await _fixture.Browser.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(address + "/").ConfigureAwait(false);
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached }).ConfigureAwait(false);
            await page.Locator(FirstRow).First.WaitForAsync().ConfigureAwait(false);
            return page;
        }

        private static async Task SwapColumnsAsync(IPage page)
        {
            await page.Locator(".polhem-grid thead .mud-drop-item").First.DragToAsync(page.Locator(".polhem-grid thead .mud-drop-zone").Nth(1)).ConfigureAwait(false);
            await Assertions.Expect(page.Locator(Headers).First).ToContainTextAsync("Qty").ConfigureAwait(false);
        }

        private static async Task<float> WidthAsync(IPage page)
            => (await page.Locator(Headers).First.BoundingBoxAsync().ConfigureAwait(false))!.Width;

        // The resizer tracks the mouse only after the server has answered, so a slow link needs the button held first.
        private static async Task DragResizerAsync(IPage page, float distance, int holdMilliseconds)
        {
            var handle = (await page.Locator(".polhem-grid thead .mud-resizer").First.BoundingBoxAsync().ConfigureAwait(false))!;
            await page.Mouse.MoveAsync(handle.X + (handle.Width / 2), handle.Y + (handle.Height / 2)).ConfigureAwait(false);
            await page.Mouse.DownAsync().ConfigureAwait(false);
            await page.WaitForTimeoutAsync(holdMilliseconds).ConfigureAwait(false);
            await page.Mouse.MoveAsync(handle.X + distance, handle.Y, new MouseMoveOptions { Steps = 5 }).ConfigureAwait(false);
            await page.Mouse.UpAsync().ConfigureAwait(false);
        }

        [BrowserFact]
        [DisplayName("Dragging a header moves the column, and its cells with it")]
        public async Task DragHeader_MovesColumnAndCells()
        {
            var page = await OpenAsync(_fixture.Address);

            await SwapColumnsAsync(page);

            await Assertions.Expect(page.Locator(Headers).Nth(1)).ToContainTextAsync("Item");
            await Assertions.Expect(page.Locator(FirstRow).First.Locator("td").First).ToHaveTextAsync("1");
            await Assertions.Expect(page.Locator(FirstRow).First.Locator("td").Nth(1)).ToHaveTextAsync("Bolt");
        }

        [BrowserFact]
        [DisplayName("After moving a column, typing writes the field under the header and Tab follows the shown order")]
        public async Task AfterMove_EditingFollowsTheShownOrder()
        {
            var page = await OpenAsync(_fixture.Address);
            await SwapColumnsAsync(page);

            await page.Locator(FirstRow).First.Locator("td").First.ClickAsync();
            await page.Keyboard.TypeAsync("55");
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator(FirstRow).First.Locator("td").Nth(1).Locator("input")).ToBeFocusedAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Locator("#outside").ClickAsync();

            await Assertions.Expect(page.Locator("#data")).ToHaveTextAsync("Washer|55;Nut|2");
        }

        [BrowserFact]
        [DisplayName("Dragging the edge of a header changes the width, and editing keeps it")]
        public async Task DragEdge_ChangesWidthAndEditingKeepsIt()
        {
            var page = await OpenAsync(_fixture.Address);
            float before = await WidthAsync(page);

            await DragResizerAsync(page, 80, 300);

            await Assertions.Expect(page.Locator(Headers).First).Not.ToHaveCSSAsync("width", $"{before}px");
            float resized = await WidthAsync(page);
            Assert.True(resized > before + 40, $"The width went from {before} to {resized}.");
            await page.Locator(FirstRow).First.Locator("td").First.ClickAsync();
            await page.Keyboard.TypeAsync("Washer");
            await page.Locator("#outside").ClickAsync();
            await Assertions.Expect(page.Locator("#data")).ToHaveTextAsync("Washer|1;Nut|2");
            // NOTE: The table lays itself out again around an open editor, which moves the width by a few pixels.
            Assert.InRange(await WidthAsync(page), resized - 20, resized + 20);
        }

        [BrowserFact]
        [DisplayName("A column does not get narrower than its layout width")]
        public async Task DragEdge_StopsAtLayoutWidth()
        {
            var page = await OpenAsync(_fixture.Address);

            await DragResizerAsync(page, -620, 300);

            await Assertions.Expect(page.Locator(Headers).First).ToHaveCSSAsync("width", new Regex("^12[0-9]"));
        }

        [BrowserFact]
        [DisplayName("Over a slow link, moving and resizing a column still work when the button is held for the round trip")]
        public async Task SlowLink_MoveAndResizeWork()
        {
            var page = await OpenAsync(_fixture.SlowAddress);
            await Assertions.Expect(page.Locator(".polhem-grid thead .mud-drop-item-draggable").First).ToBeVisibleAsync();

            await SwapColumnsAsync(page);
            await page.Locator(FirstRow).First.Locator("td").First.ClickAsync();
            await page.Keyboard.TypeAsync("55");
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator("#data")).ToHaveTextAsync("Bolt|55;Nut|2");

            float before = await WidthAsync(page);
            await DragResizerAsync(page, 80, 1500);
            await Assertions.Expect(page.Locator(Headers).First).Not.ToHaveCSSAsync("width", $"{before}px");
        }
    }
}
