using System.ComponentModel;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Identity;
using Polhem.Web.Blazor.Shell;

namespace Polhem.Web.Blazor.BehaviorTests
{
    /// <summary>
    /// What every UI package's shell must do. A UI package derives from this class and supplies what its controls render
    /// differently; the shell's own class names are common to all of them.
    /// </summary>
    public abstract class ShellBehaviorTests : BunitContext, IAsyncLifetime
    {
        private const string Company = "input[id^='polhem-shell-company-']";
        private const string Password = "input[type=password]";
        private const string Language = "button.polhem-shell-signin__language";
        private readonly ShellTestBackend _backend = new();
        private readonly CultureScope _culture = new("en-US");

        /// <inheritdoc />
        Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

        /// <inheritdoc />
        async Task IAsyncLifetime.DisposeAsync()
        {
            _culture.Dispose();
            await DisposeAsync().ConfigureAwait(true);
        }

        /// <summary>Gets the shell's layout component.</summary>
        protected abstract Type LayoutType { get; }

        /// <summary>Gets the selector of a menu link's anchor.</summary>
        protected abstract string LinkSelector { get; }

        /// <summary>Determines whether the menu's switch is on: hidden on a wide screen.</summary>
        /// <param name="cut">The rendered layout.</param>
        /// <returns>Whether it is toggled.</returns>
        protected abstract bool IsMenuToggled(IRenderedComponent<ContainerFragment> cut);

        private IRenderedComponent<ContainerFragment> RenderLayout(Action<PolhemShellOptions>? configure = null, bool withSwitcher = true)
        {
            _backend.Register(Services, configure, withSwitcher);
            return Render(builder =>
            {
                builder.OpenComponent(0, LayoutType);
                builder.AddAttribute(1, "Body", (RenderFragment)(b => b.AddContent(0, "the page")));
                builder.CloseComponent();
            });
        }

        private static void SignIn(IRenderedComponent<ContainerFragment> cut, string? companyId = null)
        {
            if (companyId is not null) cut.Find(Company).Change(companyId);
            cut.Find(Password).Change("secret");
            cut.Find("form.polhem-login-panel").Submit();
        }

        private void SignInToMenu(IRenderedComponent<ContainerFragment> cut)
        {
            SignIn(cut);
            cut.WaitForAssertion(() => cut.Find(LinkSelector));
        }

        [Fact]
        [DisplayName("Before sign-in the layout shows the configured title, hint and defaults, and not the page")]
        public void NotSignedIn_ShowsSignIn()
        {
            var cut = RenderLayout();

            Assert.Equal("Acme App", cut.Find(".polhem-shell-signin__title").TextContent.Trim());
            Assert.Equal("Use demo / demo.", cut.Find(".polhem-shell-signin__hint").TextContent.Trim());
            Assert.Equal(ShellTestBackend.Company, cut.Find(Company).GetAttribute("value"));
            Assert.DoesNotContain("the page", cut.Markup, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Signing in sends the inputs, enters the company and shows the top bar, the menu and the page")]
        public void SignIn_ShowsShell()
        {
            var cut = RenderLayout();

            SignInToMenu(cut);

            Assert.Equal(("demo", "secret"), _backend.LastLogin);
            Assert.Equal("Acme App", cut.Find(".polhem-shell__title").TextContent.Trim());
            Assert.Equal("Demo User · Acme Corp", cut.Find(".polhem-shell__user").TextContent.Trim());
            Assert.Equal("Sales", cut.Find(".polhem-shell-nav__header").TextContent.Trim());
            Assert.Equal("Orders", cut.Find(LinkSelector).TextContent.Trim());
            Assert.Equal("form/Order", cut.Find(LinkSelector).GetAttribute("href"));
            Assert.Contains("the page", cut.Find(".polhem-shell__main").TextContent, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A company the server refuses shows the error and signs out")]
        public void SignIn_Refused_ShowsErrorAndSignsOut()
        {
            var cut = RenderLayout();

            SignIn(cut, "OTHER");

            cut.WaitForAssertion(() => Assert.Contains("Company access denied",
                cut.Find(".polhem-shell-signin__error").TextContent, StringComparison.Ordinal));
            Assert.Null(Services.GetRequiredService<BlazorUserContext>().User);
            Assert.Empty(cut.FindAll(".polhem-shell__header"));
        }

        [Fact]
        [DisplayName("An empty company is reported without asking the server")]
        public void SignIn_EmptyCompany_ShowsError()
        {
            var cut = RenderLayout();

            SignIn(cut, "  ");

            cut.WaitForAssertion(() => Assert.Contains("Enter a company ID.",
                cut.Find(".polhem-shell-signin__error").TextContent, StringComparison.Ordinal));
            Assert.Equal(0, _backend.EnterCompanyCount);
        }

        [Fact]
        [DisplayName("Signing out returns to the sign-in screen and clears the user")]
        public void SignOut_ReturnsToSignIn()
        {
            var cut = RenderLayout();
            SignInToMenu(cut);

            cut.Find("button.polhem-shell__signout").Click();

            cut.WaitForAssertion(() => cut.Find(".polhem-shell-signin"));
            Assert.Null(Services.GetRequiredService<BlazorUserContext>().User);
        }

        [Fact]
        [DisplayName("The menu button toggles the menu, and picking a form clears the toggle")]
        public void MenuButton_TogglesAndNavigationClears()
        {
            var cut = RenderLayout();
            SignInToMenu(cut);
            Assert.False(IsMenuToggled(cut));

            cut.Find("button.polhem-shell__menu-button").Click();
            cut.WaitForAssertion(() => Assert.True(IsMenuToggled(cut)));

            cut.Find(LinkSelector).Click();
            cut.WaitForAssertion(() => Assert.False(IsMenuToggled(cut)));
        }

        [Fact]
        [DisplayName("The language switch offers each language and disables the current one")]
        public void Languages_Offered()
        {
            var cut = RenderLayout();

            var buttons = cut.FindAll(Language);
            Assert.Equal(["English", "繁體中文"], buttons.Select(b => b.TextContent.Trim()));
            Assert.True(buttons[0].HasAttribute("disabled"));
            Assert.False(buttons[1].HasAttribute("disabled"));
        }

        [Fact]
        [DisplayName("Picking a language asks the host's switch")]
        public void Languages_Pick_CallsSwitcher()
        {
            var cut = RenderLayout();

            cut.FindAll(Language)[1].Click();

            Assert.Equal(["zh-TW"], _backend.SwitchedTo);
        }

        [Fact]
        [DisplayName("A single language hides the switch")]
        public void Languages_Single_Hidden()
        {
            var cut = RenderLayout(options =>
            {
                options.Languages.Clear();
                options.Languages.Add(new PolhemShellLanguage("en-US", "English"));
            });

            Assert.Empty(cut.FindAll(".polhem-shell-signin__languages"));
        }

        [Fact]
        [DisplayName("A host with no language switch offers none")]
        public void Languages_NoSwitcher_Hidden()
        {
            var cut = RenderLayout(withSwitcher: false);

            Assert.Empty(cut.FindAll(".polhem-shell-signin__languages"));
        }

        /// <summary>Gets the shell's menu component.</summary>
        protected abstract Type NavMenuType { get; }

        private IRenderedComponent<ContainerFragment> RenderMenu()
        {
            var type = NavMenuType;
            return Render(b =>
            {
                b.OpenComponent(0, type);
                b.CloseComponent();
            });
        }

        [Fact]
        [DisplayName("The home page opens the menu's first form under the configured route")]
        public void Home_OpensFirstForm()
        {
            _backend.Register(Services, options => options.FormRoute = "forms");
            var navigation = Services.GetRequiredService<NavigationManager>();

            Render<PolhemShellHome>();

            Assert.Equal("http://localhost/forms/Order", navigation.Uri);
        }

        [Fact]
        [DisplayName("A menu that cannot be loaded leaves the home page empty and shows the error in the menu")]
        public void Home_MenuFails_ShowsErrorInMenu()
        {
            _backend.MenuFailure = "Menu unavailable";
            _backend.Register(Services);
            var navigation = Services.GetRequiredService<NavigationManager>();

            var home = Render<PolhemShellHome>();
            var menu = RenderMenu();

            Assert.Equal("http://localhost/", navigation.Uri);
            Assert.Empty(home.FindAll(".polhem-shell__loading"));
            Assert.Empty(home.FindAll(".polhem-shell__empty"));
            menu.WaitForAssertion(() => Assert.Contains("Menu unavailable", menu.Find(".polhem-shell-nav__error").TextContent, StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A menu with no form shows the empty note on the home page and in the menu, without navigating")]
        public void Home_EmptyMenu_ShowsNote()
        {
            var settings = new Polhem.Definition.Settings.MenuSettings();
            settings.Items!.Add(new Polhem.Definition.Settings.MenuFolder("sales", "Sales"));
            _backend.Menu = settings;
            _backend.Register(Services);
            var navigation = Services.GetRequiredService<NavigationManager>();

            var home = Render<PolhemShellHome>();
            var menu = RenderMenu();

            Assert.Equal("http://localhost/", navigation.Uri);
            Assert.False(string.IsNullOrWhiteSpace(home.Find(".polhem-shell__empty").TextContent));
            Assert.Equal(home.Find(".polhem-shell__empty").TextContent, menu.Find(".polhem-shell-nav__empty").TextContent);
        }
    }
}
