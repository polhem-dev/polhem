using Polhem.Samples.Shared;
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

        // Blazor component services — UseLocalProvider makes the PolhemApiConnectorFactory build
        // connectors that dispatch to the backend registered above, in this process.
        builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());

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
