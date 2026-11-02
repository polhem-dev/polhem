using System.ComponentModel;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BehaviorTests;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// What the shell does only in a real browser: submitting with Enter after typing, what a password manager sees, and
    /// the menu sliding over the page on a narrow screen.
    /// </summary>
    public abstract class ShellTests
    {
        private const string Drawer = ".polhem-shell__nav";
        private readonly BrowserSiteFixture _fixture;
        private readonly ShellTestBackend _shellBackend;

        /// <summary>
        /// Initializes a new instance of <see cref="ShellTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="shellBackend">The backend of the site's shell.</param>
        protected ShellTests(BrowserSiteFixture fixture, ShellTestBackend shellBackend)
        {
            _fixture = fixture;
            _shellBackend = shellBackend;
        }

        private async Task<IPage> OpenAsync(int width = 1000)
        {
            var page = await _fixture.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 700 } }).ConfigureAwait(false);
            await page.GotoAsync(_fixture.Address + "/shell").ConfigureAwait(false);
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached }).ConfigureAwait(false);
            return page;
        }

        private static async Task SignInAsync(IPage page)
        {
            await page.Locator("input[type=password]").FillAsync("secret");
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.Locator("#shell-page")).ToBeVisibleAsync();
        }

        [BrowserFact]
        [DisplayName("Enter right after typing signs in with what was typed")]
        public async Task TypeThenEnter_SendsTypedValues()
        {
            var page = await OpenAsync();
            await page.Locator("input[autocomplete=username]").FillAsync("typed-user");

            await SignInAsync(page);

            Assert.Equal(("typed-user", "secret"), _shellBackend.LastLogin);
        }

        [BrowserFact]
        [DisplayName("A password manager can tell the user-id and password inputs apart")]
        public async Task Inputs_CarryAutocomplete()
        {
            var page = await OpenAsync();

            await Assertions.Expect(page.Locator("input[autocomplete=username]")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator("input[type=password][autocomplete=current-password]")).ToHaveCountAsync(1);
        }

        [BrowserFact]
        [DisplayName("On a wide screen the menu shows, and the button hides it")]
        public async Task Wide_ButtonHidesMenu()
        {
            var page = await OpenAsync();
            await SignInAsync(page);
            await Assertions.Expect(page.Locator(".polhem-shell-nav a")).ToBeInViewportAsync();

            await page.Locator(".polhem-shell__menu-button").ClickAsync();

            await Assertions.Expect(page.Locator(".polhem-shell-nav a")).Not.ToBeInViewportAsync();
        }

        [BrowserFact]
        [DisplayName("On a narrow screen the menu starts hidden, the button opens it, and picking a form closes it")]
        public async Task Narrow_PickingFormClosesMenu()
        {
            var page = await OpenAsync(width: 500);
            await SignInAsync(page);
            await Assertions.Expect(page.Locator(".polhem-shell-nav a")).Not.ToBeInViewportAsync();

            await page.Locator(".polhem-shell__menu-button").ClickAsync();
            await Assertions.Expect(page.Locator(".polhem-shell-nav a")).ToBeInViewportAsync();
            await page.Locator(".polhem-shell-nav a").ClickAsync();

            await Assertions.Expect(page.Locator(".polhem-shell-nav a")).Not.ToBeInViewportAsync();
            await page.WaitForURLAsync("**/form/Order");
        }

        [BrowserFact]
        [DisplayName("On a narrow screen a click on the page beside the open menu closes it")]
        public async Task Narrow_OverlayClickClosesMenu()
        {
            var page = await OpenAsync(width: 500);
            await SignInAsync(page);
            await page.Locator(".polhem-shell__menu-button").ClickAsync();
            await Assertions.Expect(page.Locator(Drawer + " a")).ToBeInViewportAsync();

            await page.Mouse.ClickAsync(450, 400);

            await Assertions.Expect(page.Locator(Drawer + " a")).Not.ToBeInViewportAsync();
        }
    }
}
