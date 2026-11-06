using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BehaviorTests;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// Runs a Blazor Server site of a UI package's test pages, and a headless Chrome for the tests of one class.
    /// </summary>
    public abstract class BrowserSiteFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private IPlaywright? _playwright;
        private SlowTcpProxy? _proxy;

        /// <summary>Gets the backend of the site's form page.</summary>
        public FormPageTestBackend Backend { get; } = new(amountExpression: "price * qty");

        /// <summary>Gets the address of the site.</summary>
        public string Address { get; private set; } = string.Empty;

        /// <summary>Gets the address of the site over a slow link, in the range of a distant server.</summary>
        public string SlowAddress => _proxy!.Address;

        /// <summary>Gets the browser.</summary>
        public IBrowser Browser { get; private set; } = default!;

        /// <summary>Gets the test assembly that holds the site's pages and static web assets.</summary>
        protected abstract Assembly SiteAssembly { get; }

        /// <summary>Registers the services of the UI package.</summary>
        /// <param name="services">The service collection.</param>
        protected abstract void AddUIServices(IServiceCollection services);

        /// <summary>Maps the site's root component in interactive server mode.</summary>
        /// <param name="app">The application.</param>
        protected abstract void MapSite(WebApplication app);

        /// <summary>Registers the backend of the site's pages.</summary>
        /// <param name="services">The service collection.</param>
        protected virtual void RegisterBackend(IServiceCollection services) => Backend.Register(services);

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = SiteAssembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Development,
            });
            builder.WebHost.UseStaticWebAssets();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            AddUIServices(builder.Services);
            RegisterBackend(builder.Services);
            _app = builder.Build();
            _app.UseStaticFiles();
            _app.UseAntiforgery();
            MapSite(_app);
            await _app.StartAsync().ConfigureAwait(false);
            Address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            var uri = new Uri(Address);
            _proxy = new SlowTcpProxy(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(uri.Host), uri.Port), TimeSpan.FromMilliseconds(250));

            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true })
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            if (Browser is not null) await Browser.DisposeAsync().ConfigureAwait(false);
            _playwright?.Dispose();
            if (_proxy is not null) await _proxy.DisposeAsync().ConfigureAwait(false);
            if (_app is not null) await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
