using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Polhem.Northwind.BlazorWasm;
using Polhem.Northwind.Demo;
using Polhem.Web.Blazor.Mud.DependencyInjection;
using Polhem.Web.Blazor.Shell;
using Polhem.Web.Blazor.Wasm.DependencyInjection;
using Polhem.Web.Blazor.Wasm.Shell;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// An empty endpoint is the same-origin one, the demo site's layout. The development server sets it in
// wwwroot/appsettings.Development.json, because Polhem.Northwind.Server runs on another port there. The API key is
// public, as everything in this bundle is; it names the application and protects nothing.
var northwind = builder.Configuration.GetSection("Northwind");
string endpoint = northwind["Endpoint"] is { Length: > 0 } configured
    ? configured
    : new Uri(new Uri(builder.HostEnvironment.BaseAddress), "/api").AbsoluteUri;

builder.Services.AddPolhemBlazor(options =>
{
    options.Endpoint = endpoint;
    options.ApiKey = northwind["ApiKey"] ?? string.Empty;
});
builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "Polhem.Northwind";
    // demo-tw is left out: this head signs every account in under the language the sign-in switch shows.
    shell.SignInHint = "Use demo / demo, or demo-ro / demo for the read-only account.";
    // This head references no backend, so it does not share NorthwindCredentials.CompanyId. Change both together.
    shell.DefaultCompanyId = "NORTHWIND";
    if (NorthwindDemoSite.IsUnder(new Uri(builder.HostEnvironment.BaseAddress).AbsolutePath, NorthwindDemoSite.BlazorWasmPath))
    {
        // Served by the demo site, next to the other front ends; the sign-in screen leaves out the link to itself.
        shell.SignInLinksTitle = NorthwindDemoSite.LinksTitle;
        foreach (var (text, href) in NorthwindDemoSite.FrontEnds)
            shell.SignInLinks.Add(new PolhemShellLink(text, href));
    }
});
builder.Services.AddPolhemMud();

var host = builder.Build();
host.Services.ApplyPolhemShellCulture();
await host.RunAsync();
