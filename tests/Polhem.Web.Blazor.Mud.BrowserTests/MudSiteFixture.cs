using System.Reflection;
using Polhem.Web.Blazor.BrowserTests;
using Polhem.Web.Blazor.Mud.BrowserTests.Components;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// The test site on the MudBlazor UI package.
    /// </summary>
    public class MudSiteFixture : BrowserSiteFixture
    {
        /// <inheritdoc />
        protected override Assembly SiteAssembly => typeof(MudSiteFixture).Assembly;

        /// <inheritdoc />
        protected override void AddUIServices(IServiceCollection services) => services.AddPolhemMud();

        /// <inheritdoc />
        protected override void MapSite(WebApplication app) => app.MapRazorComponents<TestApp>().AddInteractiveServerRenderMode();
    }
}
