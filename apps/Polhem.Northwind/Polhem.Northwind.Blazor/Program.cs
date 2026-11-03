using Polhem.Northwind.Backend;
using Polhem.Northwind.Blazor.Components;
using Polhem.Northwind.Blazor.Demo;
using Polhem.Northwind.Demo;
using Polhem.Web.Blazor.Fluent.DependencyInjection;
using Polhem.Web.Blazor.Mud.DependencyInjection;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;
using Polhem.Web.Blazor.Shell;

var builder = WebApplication.CreateBuilder(args);

// Local, the default, runs the Northwind backend in this process, as a Blazor Server site usually does: the same
// definitions and the same database as Polhem.Northwind.Server, which serves the Avalonia heads. Remote calls a
// running Polhem.Northwind.Server over JSON-RPC instead, with the demo's API key. Local treats every call as
// trusted (see PolhemBlazorProviderMode.Local); the form permissions are still checked against the signed-in user.
var northwind = builder.Configuration.GetSection("Northwind");
bool isRemote = string.Equals(northwind["Mode"], "Remote", StringComparison.OrdinalIgnoreCase);

// The Demo environment is the public demo site: this host also serves the JSON-RPC endpoint and the Avalonia WASM
// bundle, same-origin (see NorthwindDemo). The endpoint needs the backend in this process.
bool isDemo = builder.Environment.IsEnvironment(NorthwindDemo.EnvironmentName);
if (isDemo && isRemote)
{
    throw new InvalidOperationException(
        "The Demo environment serves the JSON-RPC endpoint from this process and needs Northwind:Mode=Local.");
}

if (isRemote)
{
    builder.Services.AddPolhemBlazor(options => options.UseRemoteProvider(
        northwind["Endpoint"] ?? string.Empty, northwind["ApiKey"] ?? string.Empty));
}
else
{
    // Before the backend is added: it falls back to the demo key published in source when none is set.
    if (isDemo)
        NorthwindDemo.EnsureMasterKey();
    builder.AddNorthwindBackend();
    builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
    if (isDemo)
        builder.AddNorthwindDemo();
}

builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "Polhem.Northwind";
    // demo-tw is left out: this head signs every account in under the language the sign-in switch shows.
    shell.SignInHint = "Use demo / demo, or demo-ro / demo for the read-only account.";
    shell.DefaultCompanyId = NorthwindCredentials.CompanyId;
    if (isDemo)
    {
        // The Fluent UI head below shares these options, and each sign-in screen leaves out the link to itself.
        shell.SignInLinksTitle = NorthwindDemoSite.LinksTitle;
        foreach (var (text, href) in NorthwindDemoSite.FrontEnds)
            shell.SignInLinks.Add(new PolhemShellLink(text, href));
    }
});

builder.Services.AddPolhemMud();
builder.Services.AddPolhemFluent();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!isRemote)
    app.UseNorthwindBackend();
if (isDemo)
    app.UseNorthwindDemo();

// The same application on Fluent UI (Polhem.Northwind.BlazorFluent), a second Blazor app in this process. The branch
// has its own routing, so its pages, its circuit hub and its culture endpoint all sit under the path, and both heads
// share the backend, the services and the sign-in options. It is mapped first: the root app would take every path.
app.Map(NorthwindDemoSite.FluentServerPath, fluent =>
{
    fluent.UsePolhemShellLocalization();
    fluent.UseStaticFiles();
    fluent.UseRouting();
    fluent.UseAntiforgery();
    fluent.UseEndpoints(endpoints =>
    {
        endpoints.MapPolhemShellCulture();
        endpoints.MapRazorComponents<Polhem.Northwind.BlazorFluent.Components.App>().AddInteractiveServerRenderMode();
    });
});

app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
