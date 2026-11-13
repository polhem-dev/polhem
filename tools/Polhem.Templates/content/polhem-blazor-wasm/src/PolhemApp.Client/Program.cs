using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
#if (ui == "fluent")
using Polhem.Web.Blazor.Fluent.DependencyInjection;
#else
using Polhem.Web.Blazor.Mud.DependencyInjection;
#endif
using Polhem.Web.Blazor.Wasm.DependencyInjection;
using PolhemApp.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// PolhemApp.Server serves this page and the API from one origin, so the API is the page's own /api.
builder.Services.AddPolhemBlazor(options =>
{
    options.Endpoint = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "api").AbsoluteUri;
    // WARNING: everything in this bundle, the key included, can be read by anyone who can open the page. The key names
    // the application and protects nothing. The server checks only that a key is sent until an API key is issued (it
    // logs a warning at startup); issue one before deploying and put it in wwwroot/appsettings.json.
    options.ApiKey = builder.Configuration["Polhem:ApiKey"] ?? string.Empty;
});
builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "PolhemApp";
    shell.DefaultCompanyId = "POLHEM_COMPANY_ID";
    shell.DefaultUserId = "admin";
    // The language of the sign-in screen until the viewer picks another one.
    shell.DefaultCulture = "POLHEM_DEFAULT_LANGUAGE";
});
#if (ui == "fluent")
builder.Services.AddPolhemFluent();
#else
builder.Services.AddPolhemMud();
#endif

var host = builder.Build();
// Starts the page in the language picked on the sign-in screen, then in the browser's, then in the default.
host.Services.ApplyPolhemShellCulture();
await host.RunAsync();
