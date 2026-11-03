using Polhem.Northwind.Backend;
using Polhem.Northwind.BlazorFluent.Components;
using Polhem.Web.Blazor.Fluent.DependencyInjection;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;

var builder = WebApplication.CreateBuilder(args);

// Runs the Northwind backend in this process, with the same definitions and the same database as the other heads.
builder.AddNorthwindBackend();
builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());

builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "Polhem.Northwind";
    // demo-tw is left out: this head signs every account in under the language the sign-in switch shows.
    shell.SignInHint = "Use demo / demo, or demo-ro / demo for the read-only account.";
    shell.DefaultCompanyId = NorthwindCredentials.CompanyId;
});

builder.Services.AddPolhemFluent();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseNorthwindBackend();
app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
