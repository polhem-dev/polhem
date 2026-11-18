using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// What the package's handling of Escape pressed with the focus on the body does while a popover is open.
    /// </summary>
    public class MudDialogEscapeTests : IClassFixture<MudSiteFixture>
    {
        private const string Dialog = ".mud-dialog.test-dismissable";
        private readonly MudSiteFixture _fixture;

        /// <summary>
        /// Initializes a new instance of <see cref="MudDialogEscapeTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        public MudDialogEscapeTests(MudSiteFixture fixture) => _fixture = fixture;

        // A browser that does not focus a clicked button, such as Safari on macOS, can leave the focus on the body while a
        // popover is open. The popover here is open from the start and takes no focus, so nothing moves it off the body.
        [BrowserFact]
        [DisplayName("Escape with the focus on the body leaves a dismissable dialog open while a popover is open")]
        public async Task EscapeOnBody_WithPopoverOpen_KeepsDialog()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address + "/dialog-popover");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await Assertions.Expect(page.Locator(".mud-popover-open .test-popover")).ToBeVisibleAsync();
            // MudBlazor moves the focus into a dialog after it opens, which would undo a blur made before that.
            await page.WaitForFunctionAsync("selector => document.activeElement?.closest(selector) != null", Dialog);
            await page.EvaluateAsync("() => document.activeElement?.blur()");
            Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement === document.body"));

            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(500);

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(1);
        }


        [BrowserFact]
        [DisplayName("Escape with the focus on the body closes a dismissable dialog while only a tooltip is open")]
        public async Task EscapeOnBody_WithTooltipOpen_ClosesDialog()
        {
            var page = await _fixture.Browser.NewPageAsync();
            await page.GotoAsync(_fixture.Address + "/dialog-popover?tooltip=true");
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await Assertions.Expect(page.Locator(".mud-popover-open.mud-tooltip")).ToBeVisibleAsync();
            await page.WaitForFunctionAsync("selector => document.activeElement?.closest(selector) != null", Dialog);
            await page.EvaluateAsync("() => document.activeElement?.blur()");
            Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement === document.body"));

            await page.Keyboard.PressAsync("Escape");

            await Assertions.Expect(page.Locator(Dialog)).ToHaveCountAsync(0);
        }
    }
}
