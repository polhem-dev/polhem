using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Polhem.Northwind.Backend;
using Polhem.Northwind.Blazor.Components;
using Polhem.Web.Blazor.Server.DependencyInjection;

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!isRemote)
    app.UseNorthwindBackend();

// A circuit's culture is fixed when it connects, so the language comes from the request-localization cookie that
// the sign-in screen's switch writes before it reloads the page.
CultureInfo[] cultures = [new("en-US"), new("zh-TW")];
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = cultures,
    SupportedUICultures = cultures,
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/culture", (string culture, string? redirectUri, HttpContext context) =>
{
    if (cultures.Any(c => c.Name == culture))
    {
        context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)));
    }
    // Only a path on this site: an absolute or protocol-relative address would make this an open redirect.
    bool isLocal = redirectUri is { Length: > 0 } && redirectUri[0] == '/'
        && !redirectUri.StartsWith("//", StringComparison.Ordinal) && !redirectUri.StartsWith("/\\", StringComparison.Ordinal);
    return Results.LocalRedirect(isLocal ? redirectUri! : "/");
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
