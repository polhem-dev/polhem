using System.ComponentModel;
using Microsoft.Playwright;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// What resizing and moving a column does only in a real browser: native dragging, and what editing does afterwards.
    /// </summary>
    public abstract class GridColumnTests
    {
        private const string Headers = ".polhem-grid thead th";
        private const string FirstRow = ".polhem-grid tbody tr";
        private const float LayoutWidth = 120;
        private readonly BrowserSiteFixture _fixture;

        /// <summary>Gets the selector of what the user drags to move a column.</summary>
        protected abstract string ColumnDragItem { get; }

        /// <summary>Gets the selector of where a dragged column lands.</summary>
        protected abstract string ColumnDropZone { get; }

        /// <summary>Gets the selector of the handle that resizes a column.</summary>
        protected abstract string ColumnResizer { get; }

        /// <summary>Gets the selector of a column header once it can be dragged.</summary>
        protected abstract string DraggableColumn { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="GridColumnTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        protected GridColumnTests(BrowserSiteFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenAsync(string address)
        {
            var page = await _fixture.Browser.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(address + "/").ConfigureAwait(false);
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached }).ConfigureAwait(false);
            await page.Locator(FirstRow).First.WaitForAsync().ConfigureAwait(false);
            return page;
        }

        /// <summary>Waits until a column header can be dragged, past what its markup shows.</summary>
        /// <param name="page">The page.</param>
        /// <returns>The operation.</returns>
        protected virtual Task WaitForColumnsReadyAsync(IPage page) => Task.CompletedTask;

        private async Task SwapColumnsAsync(IPage page)
        {
            await page.Locator(ColumnDragItem).First.DragToAsync(page.Locator(ColumnDropZone).Nth(1)).ConfigureAwait(false);
            await Assertions.Expect(page.Locator(Headers).First).ToContainTextAsync("Qty").ConfigureAwait(false);
        }

        private static async Task<float> WidthAsync(IPage page)
            => (await page.Locator(Headers).First.BoundingBoxAsync().ConfigureAwait(false))!.Width;

        // The resizer tracks the mouse only after the server has answered, so a slow link needs the button held first.
        private async Task DragResizerAsync(IPage page, float distance, int holdMilliseconds)
        {
            var handle = (await page.Locator(ColumnResizer).First.BoundingBoxAsync().ConfigureAwait(false))!;
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

            // The width follows the moves as they reach the server, so an early reading is only part of the way there.
            for (int attempt = 0; attempt < 50 && await WidthAsync(page) < before + 60; attempt++)
                await page.WaitForTimeoutAsync(100);
            Assert.True(await WidthAsync(page) >= before + 60, $"The width started at {before} and did not reach its target.");
            await page.WaitForTimeoutAsync(300);
            float resized = await WidthAsync(page);
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
            float before = await WidthAsync(page);

            await DragResizerAsync(page, -620, 300);

            // A table can give a column more than was asked for, so this holds the rule and not one framework's number.
            for (int attempt = 0; attempt < 50 && await WidthAsync(page) >= before; attempt++)
                await page.WaitForTimeoutAsync(100);
            await page.WaitForTimeoutAsync(300);
            float width = await WidthAsync(page);
            Assert.True(width < before, $"The width started at {before} and did not narrow.");
            Assert.True(width >= LayoutWidth, $"The column narrowed to {width}, under its layout width of {LayoutWidth}.");
        }

        [BrowserFact]
        [DisplayName("Over a slow link, moving and resizing a column still work when the button is held for the round trip")]
        public async Task SlowLink_MoveAndResizeWork()
        {
            var page = await OpenAsync(_fixture.SlowAddress);
            await Assertions.Expect(page.Locator(DraggableColumn).First).ToBeVisibleAsync();
            await WaitForColumnsReadyAsync(page);

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
