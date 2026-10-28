using System.ComponentModel;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Exceptions;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Identity;
using Polhem.Web.Blazor.Server.Shell;

namespace Polhem.Web.Blazor.Server.UnitTests.Shell
{
    /// <summary>
    /// Renders <see cref="PolhemShellLayout"/> with bUnit against a <see cref="FakeApiServer"/>: the sign-in screen
    /// until a company is entered, then the top bar, the menu and the page, and back to the sign-in on sign-out.
    /// </summary>
    /// <remarks>
    /// The services are those of <see cref="PolhemBlazorServiceCollectionExtensions.AddPolhemBlazor"/> and
    /// <see cref="PolhemShellServiceCollectionExtensions.AddPolhemShell"/>, with the circuit's client replaced by one
    /// whose calls go to the fake. The definition loader is off, so the menu takes a single call. Each test pins
    /// <c>en-US</c>, the culture the expected text is written in.
    /// </remarks>
    public sealed class PolhemShellLayoutTests : BunitContext
    {
        private const string Company = "ACME";

        private readonly CultureScope _culture = new("en-US");

        private static MenuSettings BuildMenu()
        {
            var settings = new MenuSettings();
            var folder = new MenuFolder("sales", "Sales");
            folder.Items!.Add(new MenuEntry("order", "Order", "Orders"));
            settings.Items!.Add(folder);
            return settings;
        }

        private FakeApiServer Register(Action<PolhemShellOptions>? configure = null)
        {
            var server = new FakeApiServer()
                .On<LoginRequest>($"{SysProgIds.System}.{SystemActions.Login}", request => new LoginResponse
                {
                    AccessToken = Guid.NewGuid(),
                    UserId = request.UserId,
                    UserName = "Demo User",
                })
                .On<EnterCompanyRequest>($"{SysProgIds.System}.{SystemActions.EnterCompany}", request =>
                    request.CompanyId == Company
                        ? new EnterCompanyResponse { Company = new CompanyInfo { CompanyId = Company, CompanyName = "Acme Corp" } }
                        : throw new UserMessageException("Company access denied"))
                .On<GetDefineRequest>($"{SysProgIds.System}.{SystemActions.GetDefine}",
                    _ => new GetDefineResponse { Xml = XmlCodec.Serialize(BuildMenu()) });
            Services.AddPolhemBlazor(options =>
            {
                options.UseLocalProvider();
                options.UseDefinitionLoader = false;
            });
            Services.AddPolhemShell(shell =>
            {
                shell.AppTitle = "Acme App";
                shell.SignInHint = "Use demo / demo.";
                shell.DefaultCompanyId = Company;
                shell.DefaultUserId = "demo";
                configure?.Invoke(shell);
            });
            // Registered after `AddPolhemBlazor`, so this is the client the shell resolves.
            Services.AddScoped(_ => server.CreateClient());
            return server;
        }

        private IRenderedComponent<PolhemShellLayout> RenderLayout()
            => Render<PolhemShellLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddContent(0, "the page"))));

        private static void SignIn(IRenderedComponent<PolhemShellLayout> cut, string? companyId = null)
        {
            if (companyId is not null)
                cut.Find("input.polhem-login-panel__input[type=text]").Change(companyId);
            cut.Find("input[type=password]").Change("demo");
            cut.Find("form.polhem-login-panel").Submit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _culture.Dispose();
            base.Dispose(disposing);
        }

        [Fact]
        [DisplayName("Before sign-in the layout shows the sign-in screen with the configured title, hint and defaults")]
        public void Render_NotSignedIn_ShowsSignInWithOptions()
        {
            Register();

            var cut = RenderLayout();

            Assert.Equal("Acme App", cut.Find("h1.polhem-shell-signin__title").TextContent);
            Assert.Equal("Use demo / demo.", cut.Find("p.polhem-shell-signin__hint").TextContent);
            var inputs = cut.FindAll("input.polhem-login-panel__input[type=text]");
            Assert.Equal(Company, inputs[0].GetAttribute("value"));
            Assert.Equal("demo", inputs[1].GetAttribute("value"));
            Assert.DoesNotContain("the page", cut.Markup, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Signing in enters the company and shows the top bar, the menu and the page")]
        public void SignIn_ValidCompany_ShowsShell()
        {
            var server = Register();
            var cut = RenderLayout();

            SignIn(cut);

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.polhem-shell-nav li").Count));
            Assert.Equal("Acme App", cut.Find("span.polhem-shell__title").TextContent);
            Assert.Equal("Demo User · Acme Corp", cut.Find("span.polhem-shell__user").TextContent);
            Assert.Equal("Sales", cut.Find("li.polhem-shell-nav__header").TextContent);
            var link = cut.Find("a.polhem-shell-nav__link");
            Assert.Equal("Orders", link.TextContent);
            Assert.Equal("form/Order", link.GetAttribute("href"));
            Assert.Equal("the page", cut.Find("main.polhem-shell__main").TextContent);
            Assert.Equal(Company, Services.GetRequiredService<CircuitUserContext>().Company?.CompanyId);
            Assert.Contains($"{SysProgIds.System}.{SystemActions.EnterCompany}", server.Calls);
        }

        [Fact]
        [DisplayName("A company the server refuses shows the error on the sign-in screen and signs the circuit out")]
        public void SignIn_RefusedCompany_ShowsErrorAndSignsOut()
        {
            Register();
            var cut = RenderLayout();

            SignIn(cut, "OTHER");

            cut.WaitForAssertion(() => Assert.Contains("Company access denied",
                cut.Find("div.polhem-shell-signin__error").TextContent, StringComparison.Ordinal));
            var context = Services.GetRequiredService<CircuitUserContext>();
            Assert.Null(context.User);
            Assert.Null(context.Company);
            Assert.Empty(cut.FindAll("header.polhem-shell__header"));
        }

        [Fact]
        [DisplayName("An empty company is reported without calling the server to enter it")]
        public void SignIn_EmptyCompany_ShowsErrorWithoutEnterCompany()
        {
            var server = Register();
            var cut = RenderLayout();

            SignIn(cut, "  ");

            cut.WaitForAssertion(() => Assert.Equal("Enter a company ID.",
                cut.Find("div.polhem-shell-signin__error").TextContent));
            Assert.DoesNotContain($"{SysProgIds.System}.{SystemActions.EnterCompany}", server.Calls);
            Assert.Null(Services.GetRequiredService<CircuitUserContext>().User);
        }

        [Fact]
        [DisplayName("Signing out returns to the sign-in screen and clears the circuit's user")]
        public void SignOut_ReturnsToSignIn()
        {
            Register();
            var cut = RenderLayout();
            SignIn(cut);
            cut.WaitForAssertion(() => cut.Find("button.polhem-shell__signout"));

            cut.Find("button.polhem-shell__signout").Click();

            cut.WaitForAssertion(() => cut.Find("section.polhem-shell-signin"));
            Assert.Null(Services.GetRequiredService<CircuitUserContext>().User);
        }

        [Fact]
        [DisplayName("The menu button toggles the menu's modifier class, and picking a form clears it")]
        public void MenuButton_TogglesAndNavigationClears()
        {
            Register();
            var cut = RenderLayout();
            SignIn(cut);
            cut.WaitForAssertion(() => cut.Find("a.polhem-shell-nav__link"));

            cut.Find("button.polhem-shell__menu-button").Click();
            Assert.Contains("polhem-shell__nav--toggled", cut.Find("nav").ClassList);

            cut.Find("a.polhem-shell-nav__link").Click();
            Assert.DoesNotContain("polhem-shell__nav--toggled", cut.Find("nav").ClassList);
        }

        [Fact]
        [DisplayName("The language switch offers each configured language and disables the current one")]
        public void LanguageSwitch_OffersConfiguredLanguages()
        {
            Register();

            var cut = RenderLayout();

            var buttons = cut.FindAll("button.polhem-shell-signin__language");
            Assert.Equal(["English", "繁體中文"], buttons.Select(b => b.TextContent));
            Assert.True(buttons[0].HasAttribute("disabled"));
            Assert.False(buttons[1].HasAttribute("disabled"));
        }

        [Fact]
        [DisplayName("Picking a language reloads the page through the culture endpoint")]
        public void LanguageSwitch_NavigatesThroughCultureEndpoint()
        {
            Register();
            var navigation = Services.GetRequiredService<NavigationManager>();
            navigation.NavigateTo("form/Order");
            var cut = RenderLayout();

            cut.FindAll("button.polhem-shell-signin__language")[1].Click();

            Assert.Equal("http://localhost/culture?culture=zh-TW&redirectUri=%2Fform%2FOrder", navigation.Uri);
        }

        [Fact]
        [DisplayName("A single language hides the language switch")]
        public void LanguageSwitch_SingleLanguage_IsHidden()
        {
            Register(shell =>
            {
                shell.Languages.Clear();
                shell.Languages.Add(new PolhemShellLanguage("en-US", "English"));
            });

            var cut = RenderLayout();

            Assert.Empty(cut.FindAll("div.polhem-shell-signin__languages"));
        }
    }
}
