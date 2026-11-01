using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Playwright;
using Polhem.Web.Blazor.BehaviorTests;
using Polhem.Web.Blazor.Mud.BrowserTests.Components;
using Polhem.Web.Blazor.Mud.DependencyInjection;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Runs a Blazor Server site over <see cref="GridPage"/> and a form page, and a headless Chrome for the tests of one class.
    /// </summary>
    public class GridBrowserFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private IPlaywright? _playwright;
        private SlowTcpProxy? _proxy;

        /// <summary>Gets the backend of the site's form page.</summary>
        public FormPageTestBackend Backend { get; } = new();

        /// <summary>Gets the address of the site.</summary>
        public string Address { get; private set; } = string.Empty;

        /// <summary>Gets the address of the site over a slow link, in the range of a distant server.</summary>
        public string SlowAddress => _proxy!.Address;

        /// <summary>Gets the browser.</summary>
        public IBrowser Browser { get; private set; } = default!;

        /// <summary>Registers the backend of the site's pages.</summary>
        /// <param name="services">The service collection.</param>
        protected virtual void RegisterBackend(IServiceCollection services) => Backend.Register(services);

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(GridBrowserFixture).Assembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Development,
            });
            builder.WebHost.UseStaticWebAssets();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddPolhemMud();
            RegisterBackend(builder.Services);
            _app = builder.Build();
            _app.UseStaticFiles();
            _app.UseAntiforgery();
            _app.MapRazorComponents<TestApp>().AddInteractiveServerRenderMode();
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
