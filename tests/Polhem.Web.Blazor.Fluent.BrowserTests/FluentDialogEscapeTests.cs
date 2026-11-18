using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// What the Escape guard of a dialog that may not be dismissed does to another dialog that is not placed inside it.
    /// </summary>
    public class FluentDialogEscapeTests : IClassFixture<FluentSiteFixture>
    {
        private const string Guarded = "fluent-dialog.test-guarded";
        private const string Sibling = "fluent-dialog.test-sibling";
        private readonly FluentSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="FluentDialogEscapeTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public FluentDialogEscapeTests(FluentSiteFixture fixture) => _fixture = fixture;

        [BrowserFact]
        [DisplayName("Escape with the focus on the body closes a dismissable dialog opened above a guarded one, and the guarded one stays open")]
        public async Task EscapeOnBody_ClosesTheLaterSibling()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address + "/dialog-order");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.Locator("#open-sibling").ClickAsync();
            await Assertions.Expect(page.Locator("#in-sibling")).ToBeVisibleAsync();
            await BlurAsync(page);

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Sibling)).ToHaveCountAsync(0);
            // Closing the sibling hands the focus back to the button that opened it, inside the guarded dialog.
            await page.WaitForTimeoutAsync(100);
            await BlurAsync(page);
            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(500);
            await Assertions.Expect(page.Locator($"{Guarded} #open-sibling")).ToBeVisibleAsync();
        }

        private static async Task BlurAsync(IPage page)
        {
            await page.EvaluateAsync("() => document.activeElement?.blur()");
            Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement === document.body"));
        }
    }
}
