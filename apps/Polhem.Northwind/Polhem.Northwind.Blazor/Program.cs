using Polhem.Northwind.Backend;
using Polhem.Northwind.Blazor.Components;
using Polhem.Northwind.Blazor.Demo;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

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
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!isRemote)
    app.UseNorthwindBackend();
if (isDemo)
    app.UseNorthwindDemo();

app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
