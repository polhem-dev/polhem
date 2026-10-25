using Polhem.Api.Contracts.Form;
using Polhem.Samples.Shared;
using Polhem.UI.Core.Definitions;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Blazor.Server.Demo.Components;

namespace Blazor.Server.Demo;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Polhem backend services (in-process JSON-RPC dispatch) — must run before
        // AddPolhemBlazor so the Local provider has services to resolve.
        builder.AddPolhemBackend();

        // Blazor component services — UseLocalProvider gives each circuit a PolhemApiClient that
        // dispatches to the backend registered above, in this process.
        builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());

        // Only for the field editor page's in-memory lookup; see DemoLookupSource. Scoped, because a
        // Blazor Server process serves several users.
        builder.Services.AddScoped<DemoLookupSource>();
        builder.Services.AddScoped<IFormDefinitionSource>(sp => sp.GetRequiredService<DemoLookupSource>());
        builder.Services.AddScoped<IFormApiConnectorFactory>(sp => sp.GetRequiredService<DemoLookupSource>());

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();
        app.UsePolhemBackend();

        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        app.Run();
    }
}
