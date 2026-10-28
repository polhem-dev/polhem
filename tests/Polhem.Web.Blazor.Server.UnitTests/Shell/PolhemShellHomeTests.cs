using System.ComponentModel;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.Messages.System;
using Polhem.Core.Exceptions;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Settings;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

namespace Polhem.Web.Blazor.Server.UnitTests.Shell
{
    /// <summary>
    /// Covers <see cref="PolhemShellHome"/> and the failure and empty paths of <see cref="PolhemShellNavMenu"/>.
    /// </summary>
    public sealed class PolhemShellHomeTests : BunitContext
    {
        private void Register(Func<GetDefineRequest, object?> getDefine)
        {
            var server = new FakeApiServer()
                .On($"{SysProgIds.System}.{SystemActions.GetDefine}", getDefine);
            Services.AddPolhemBlazor(options => options.UseDefinitionLoader = false);
            Services.AddPolhemShell(shell => shell.FormRoute = "forms");
            Services.AddScoped(_ => server.CreateClient());
        }

        [Fact]
        [DisplayName("The home page opens the menu's first form under the configured route")]
        public void Home_OpensFirstForm()
        {
            var settings = new MenuSettings();
            var folder = new MenuFolder("sales", "Sales");
            folder.Items!.Add(new MenuEntry("order", "Order", "Orders"));
            settings.Items!.Add(folder);
            Register(_ => new GetDefineResponse { Xml = XmlCodec.Serialize(settings) });
            var navigation = Services.GetRequiredService<NavigationManager>();

            Render<PolhemShellHome>();

            Assert.Equal("http://localhost/forms/Order", navigation.Uri);
        }

        [Fact]
        [DisplayName("A menu that cannot be loaded leaves the home page in place and shows the error in the menu")]
        public void MenuLoadFailure_ShowsErrorWithoutNavigating()
        {
            Register(_ => throw new UserMessageException("Menu unavailable"));
            var navigation = Services.GetRequiredService<NavigationManager>();

            Render<PolhemShellHome>();
            var menu = Render<PolhemShellNavMenu>();

            Assert.Equal("http://localhost/", navigation.Uri);
            Assert.Contains("Menu unavailable", menu.Find("div.polhem-shell-nav__error").TextContent, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A menu that cannot be loaded leaves the home page empty instead of showing it as loading")]
        public void MenuLoadFailure_HomeStopsLoading()
        {
            Register(_ => throw new UserMessageException("Menu unavailable"));

            var home = Render<PolhemShellHome>();

            Assert.Empty(home.FindAll(".polhem-shell__loading"));
            Assert.Empty(home.FindAll(".polhem-shell__empty"));
        }

        [Fact]
        [DisplayName("A menu with no form shows the empty note on the home page and in the menu, without navigating")]
        public void EmptyMenu_ShowsEmptyNoteWithoutNavigating()
        {
            var settings = new MenuSettings();
            settings.Items!.Add(new MenuFolder("sales", "Sales"));
            Register(_ => new GetDefineResponse { Xml = XmlCodec.Serialize(settings) });
            var navigation = Services.GetRequiredService<NavigationManager>();

            var home = Render<PolhemShellHome>();
            var menu = Render<PolhemShellNavMenu>();

            Assert.Equal("http://localhost/", navigation.Uri);
            Assert.Empty(home.FindAll(".polhem-shell__loading"));
            Assert.False(string.IsNullOrWhiteSpace(home.Find("p.polhem-shell__empty").TextContent));
            Assert.Empty(menu.FindAll("ul.polhem-shell-nav"));
            Assert.Equal(home.Find("p.polhem-shell__empty").TextContent, menu.Find("div.polhem-shell-nav__empty").TextContent);
        }
    }
}
