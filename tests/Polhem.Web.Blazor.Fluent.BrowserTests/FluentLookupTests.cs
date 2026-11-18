using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// Runs the shared lookup tests on the Fluent UI package, whose dialog stays inline in the form scope.
    /// </summary>
    public class FluentLookupTests : LookupTests, IClassFixture<FluentLookupFixture>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FluentLookupTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentLookupTests(FluentLookupFixture fixture) : base(fixture, fixture.Source) { }

        /// <inheritdoc />
        protected override string Dialog => "fluent-dialog.polhem-lookup-dialog";

        /// <inheritdoc />
        protected override string LookupButton => ".polhem-button-edit__search";

        /// <inheritdoc />
        protected override string RowEditDialog => "fluent-dialog.polhem-row-edit-dialog";

        // The row edit dialog's Escape guard must not take Escape from a lookup on top of it. MudBlazor closes a dialog
        // on Escape only while the focus is inside it, so this scenario is the Fluent guard's alone.
        [BrowserFact]
        [DisplayName("Escape with the focus on the body closes a lookup opened from the row edit dialog and nothing else")]
        public async Task EscapeOnBodyInRowEdit_ClosesOnlyTheLookup()
        {
            var (page, panel) = await OpenRowEditAsync();
            await panel.Locator(LookupButton).ClickAsync();
            await Assertions.Expect(page.Locator($"{Dialog} tr.polhem-grid__row")).ToHaveCountAsync(2);
            await page.WaitForFunctionAsync("selector => document.activeElement?.closest(selector) != null", Dialog);
            await BlurAsync(page);

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
            await page.WaitForTimeoutAsync(500);
            await Assertions.Expect(panel).ToBeVisibleAsync();
        }
    }
}
