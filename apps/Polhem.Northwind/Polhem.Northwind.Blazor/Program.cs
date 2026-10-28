using Polhem.Northwind.Backend;
using Polhem.Northwind.Blazor.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

var builder = WebApplication.CreateBuilder(args);

// Local, the default, runs the Northwind backend in this process, as a Blazor Server site usually does: the same
// definitions and the same database as Polhem.Northwind.Server, which serves the Avalonia heads. Remote calls a
// running Polhem.Northwind.Server over JSON-RPC instead, with the demo's API key. Local treats every call as
// trusted (see PolhemBlazorProviderMode.Local); the form permissions are still checked against the signed-in user.
var northwind = builder.Configuration.GetSection("Northwind");
bool isRemote = string.Equals(northwind["Mode"], "Remote", StringComparison.OrdinalIgnoreCase);
if (isRemote)
{
    builder.Services.AddPolhemBlazor(options => options.UseRemoteProvider(
        northwind["Endpoint"] ?? string.Empty, northwind["ApiKey"] ?? string.Empty));
}
else
{
    builder.AddNorthwindBackend();
    builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
}

builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "Polhem.Northwind";
    shell.SignInHint = "Use demo / demo.";
    shell.DefaultCompanyId = NorthwindCredentials.CompanyId;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!isRemote)
    app.UseNorthwindBackend();

app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
