using System.Reflection;
using Polhem.Web.Blazor.BrowserTests;
using Polhem.Web.Blazor.Fluent.BrowserTests.Components;
using Polhem.Web.Blazor.Fluent.DependencyInjection;

namespace Polhem.Web.Blazor.Fluent.BrowserTests
{
    /// <summary>
    /// The test site on the Fluent UI UI package.
    /// </summary>
    public class FluentSiteFixture : BrowserSiteFixture
    {
        /// <inheritdoc />
        protected override Assembly SiteAssembly => typeof(FluentSiteFixture).Assembly;

        /// <inheritdoc />
        protected override void AddUIServices(IServiceCollection services)
        {
            services.AddPolhemFluent();
            services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options => options.DetailedErrors = true);
        }

        /// <inheritdoc />
        protected override void MapSite(WebApplication app) => app.MapRazorComponents<TestApp>().AddInteractiveServerRenderMode();
    }
}
