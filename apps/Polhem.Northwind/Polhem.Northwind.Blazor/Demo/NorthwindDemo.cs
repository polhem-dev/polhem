using Microsoft.Extensions.FileProviders;
using Polhem.Hosting;
using Polhem.JsonRpc.AspNetCore;

namespace Polhem.Northwind.Blazor.Demo;

/// <summary>
/// The public demo site: this Blazor Server host, the JSON-RPC endpoint and the published Avalonia WASM bundle in
/// one process, so both front ends share one origin, one set of definitions and one database.
/// </summary>
/// <remarks>
/// It is switched on by running the host in the <see cref="EnvironmentName"/> environment
/// (<c>ASPNETCORE_ENVIRONMENT=Demo</c>), so the development host keeps serving the Blazor site alone. Serving the
/// bundle same-origin is what lets the WASM head reach <see cref="ApiPath"/> without a CORS policy.
/// </remarks>
public static class NorthwindDemo
{
    /// <summary>The hosting environment that turns the demo site on.</summary>
    public const string EnvironmentName = "Demo";

    /// <summary>The JSON-RPC endpoint, the same path <c>Polhem.Northwind.Server</c> maps.</summary>
    public const string ApiPath = "/api";

    /// <summary>
    /// Where the Avalonia WASM bundle is served.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: the Browser head's <c>Program.ResolveDefaultEndpoint</c> recognises this path to pre-fill the
    /// same-origin endpoint. Change both together.
    /// </remarks>
    public const string BrowserPath = "/web";

    private const string BundleKey = "Northwind.BrowserBundle";

    /// <summary>
    /// Registers the JSON-RPC server and the published WASM bundle.
    /// </summary>
    /// <param name="builder">The web application builder, on which the Northwind backend is already registered.</param>
    /// <remarks>
    /// The bundle directory is <c>Northwind:BrowserBundle</c>, resolved against the content root, and defaults to
    /// <c>browser</c> under it. It is the <c>wwwroot</c> folder that <c>dotnet publish</c> writes for
    /// <c>Polhem.Northwind.Browser</c>. A missing bundle stops the host here rather than turning every
    /// <see cref="BrowserPath"/> request into a 404.
    /// </remarks>
    public static void AddNorthwindDemo(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string bundle = Path.GetFullPath(
            builder.Configuration["Northwind:BrowserBundle"] ?? "browser", builder.Environment.ContentRootPath);
        if (!File.Exists(Path.Combine(bundle, "index.html")))
        {
            throw new InvalidOperationException(
                $"The Avalonia WASM bundle was not found in '{bundle}'. Publish Polhem.Northwind.Browser and point " +
                "Northwind:BrowserBundle at the wwwroot folder of the publish output.");
        }

        // `dotnet run` serves the framework's and the packages' static files (`_framework/blazor.web.js`,
        // `_content/...`) from the build output only when static web assets are on, which the host does by itself
        // in Development alone. A published app has the files on disk and no manifest, so this does nothing there.
        builder.WebHost.UseStaticWebAssets();

        // Registered through a factory so the container disposes the provider with the host.
        builder.Services.AddKeyedSingleton<IFileProvider>(BundleKey, (_, _) => new PhysicalFileProvider(bundle));

        builder.Services.AddJsonRpcServer();
        builder.Services.AddPolhemApiKeyGateCheck();
    }

    /// <summary>
    /// Serves the WASM bundle under <see cref="BrowserPath"/> and maps the JSON-RPC endpoint at
    /// <see cref="ApiPath"/>.
    /// </summary>
    /// <param name="app">The built web application, after the Northwind backend has been seeded.</param>
    /// <remarks>
    /// Call it before the host's own <c>UseStaticFiles</c>: the bundle has a <c>_framework/</c> folder of its own,
    /// and only its path prefix keeps it apart from the Blazor one.
    /// </remarks>
    public static void UseNorthwindDemo(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseBrowserBundle(BrowserPath, app.Services.GetRequiredKeyedService<IFileProvider>(BundleKey));
        app.MapJsonRpc(ApiPath);
    }
}
