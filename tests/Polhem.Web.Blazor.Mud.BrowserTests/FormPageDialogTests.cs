using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BehaviorTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// What a dialog of the form page does only in a real browser: where the focus lands, what Escape does, which dialog
    /// is on top, and whether the viewport module loads from the shared package.
    /// </summary>
    public class FormPageDialogTests : IClassFixture<GridBrowserFixture>
    {
        private const string Row = "tr.polhem-grid__row";
        private readonly GridBrowserFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="FormPageDialogTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FormPageDialogTests(GridBrowserFixture fixture) => _fixture = fixture;

        private async Task<IPage> OpenListAsync()
        {
            var page = await _fixture.Browser.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(_fixture.Address + "/form").ConfigureAwait(false);
            await Assertions.Expect(page.Locator(Row)).ToHaveCountAsync(2).ConfigureAwait(false);
            // The rows are prerendered, and a click means nothing until the circuit runs the page. The viewport module
            // loads with the first interactive render.
            await page.WaitForFunctionAsync(
                "performance.getEntriesByType('resource').some(e => e.name.includes('CompactViewportObserver'))").ConfigureAwait(false);
            return page;
        }

        private static ILocator Command(IPage page, string text)
            => page.Locator(".polhem-form-page__toolbar button", new PageLocatorOptions { HasText = text });

        private async Task<IPage> OpenDeleteQuestionAsync()
        {
            var page = await OpenListAsync();
            await page.Locator(Row).First.ClickAsync();
            await Command(page, "Delete").ClickAsync();
            await Assertions.Expect(page.Locator(".polhem-confirm-dialog__cancel")).ToBeVisibleAsync();
            return page;
        }

        private async Task<IPage> OpenRelatedRecordAsync()
        {
            var page = await OpenListAsync();
            await page.Locator(Row).First.DblClickAsync();
            await page.Locator($"[id='polhem-form-{FormPageTestBackend.RelationField}']").DblClickAsync();
            await Assertions.Expect(page.Locator(".polhem-related-record-dialog__toolbar")).ToBeVisibleAsync();
            return page;
        }

        [BrowserFact]
        [DisplayName("The delete question puts the focus on Cancel, so a stray Enter does not delete")]
        public async Task DeleteQuestion_Opens_FocusesCancel()
        {
            var page = await OpenDeleteQuestionAsync();
            int deleted = _fixture.Backend.Deleted.Count;

            await Assertions.Expect(page.Locator(".polhem-confirm-dialog__cancel")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page.Locator(".polhem-confirm-dialog__cancel")).ToHaveCountAsync(0);
            Assert.Equal(deleted, _fixture.Backend.Deleted.Count);
        }

        [BrowserFact]
        [DisplayName("Escape backs out of the delete question and the list commands work again")]
        public async Task DeleteQuestion_Escape_BacksOut()
        {
            var page = await OpenDeleteQuestionAsync();
            int deleted = _fixture.Backend.Deleted.Count;

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(".polhem-confirm-dialog__cancel")).ToHaveCountAsync(0);
            await Assertions.Expect(Command(page, "Delete")).ToBeEnabledAsync();
            Assert.Equal(deleted, _fixture.Backend.Deleted.Count);
        }

        [BrowserFact]
        [DisplayName("A click outside the delete question backs out")]
        public async Task DeleteQuestion_BackdropClick_BacksOut()
        {
            var page = await OpenDeleteQuestionAsync();
            int deleted = _fixture.Backend.Deleted.Count;

            await page.Mouse.ClickAsync(5, 5);

            await Assertions.Expect(page.Locator(".polhem-confirm-dialog__cancel")).ToHaveCountAsync(0);
            Assert.Equal(deleted, _fixture.Backend.Deleted.Count);
        }

        [BrowserFact]
        [DisplayName("Delete on the question deletes the row")]
        public async Task DeleteQuestion_Confirm_Deletes()
        {
            var page = await OpenDeleteQuestionAsync();
            int deleted = _fixture.Backend.Deleted.Count;

            await page.Locator(".polhem-confirm-dialog__confirm").ClickAsync();

            await Assertions.Expect(page.Locator(".polhem-confirm-dialog__confirm")).ToHaveCountAsync(0);
            Assert.Equal(deleted + 1, _fixture.Backend.Deleted.Count);
        }

        [BrowserFact]
        [DisplayName("A related record opened from a related record stacks on top, and closing it leaves the first open")]
        public async Task RelatedRecord_Stacked_ClosesTopFirst()
        {
            var page = await OpenRelatedRecordAsync();
            var inputs = page.Locator(".mud-dialog input");
            var parent = page.Locator($"[id$='-{FormPageTestBackend.ParentField}']");

            await parent.DblClickAsync();
            await Assertions.Expect(page.Locator(".polhem-related-record-dialog__toolbar")).ToHaveCountAsync(2);

            // A click reaches only an element nothing covers, so this fails when the new dialog sits underneath.
            await page.Locator(".polhem-related-record-dialog__close").Last.ClickAsync(new LocatorClickOptions { Timeout = 5000 });

            await Assertions.Expect(page.Locator(".polhem-related-record-dialog__toolbar")).ToHaveCountAsync(1);
            await Assertions.Expect(inputs.First).ToBeVisibleAsync();
        }

        [BrowserFact]
        [DisplayName("Back from a viewed record leaves no related record dialog behind")]
        public async Task RelatedRecord_Closed_LeavesNothingOpen()
        {
            var page = await OpenRelatedRecordAsync();

            await page.Locator(".polhem-related-record-dialog__close").ClickAsync();

            await Assertions.Expect(page.Locator(".mud-dialog")).ToHaveCountAsync(0);
            await Assertions.Expect(Command(page, "Back")).ToBeEnabledAsync();
        }

        [BrowserFact]
        [DisplayName("A narrow viewport shows the record in its compact layout, through the module in the shared package")]
        public async Task NarrowViewport_RecordIsCompact()
        {
            var page = await OpenListAsync();
            await page.SetViewportSizeAsync(500, 800);
            await page.Locator(Row).First.DblClickAsync();

            await Assertions.Expect(page.Locator(".polhem-form-layout--compact")).ToHaveCountAsync(1);
        }
    }
}
