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
    /// Covers <see cref="PolhemShellHome"/> and the failure path of <see cref="PolhemShellNavMenu"/>.
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
    }
}
