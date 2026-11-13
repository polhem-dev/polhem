using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Polhem.Core;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Definition.Security;
using Polhem.JsonRpc.AspNetCore;
using Polhem.Northwind.Demo;
using Polhem.Hosting.DependencyInjection;

namespace Polhem.Northwind.Blazor.Demo;

/// <summary>
/// The public demo site: the two Blazor Server heads of this host, the JSON-RPC endpoint and the published WASM
/// bundles in one process, so every front end shares one origin, one set of definitions and one database.
/// </summary>
/// <remarks>
/// It is switched on by running the host in the <see cref="EnvironmentName"/> environment
/// (<c>ASPNETCORE_ENVIRONMENT=Demo</c>), so the development host keeps serving the Blazor Server heads alone. Serving
/// the bundles same-origin is what lets the WASM heads reach <see cref="ApiPath"/> without a CORS policy. Where each
/// front end is served is <see cref="NorthwindDemoSite"/>.
/// </remarks>
public static class NorthwindDemo
{
    /// <summary>The hosting environment that turns the demo site on.</summary>
    public const string EnvironmentName = "Demo";

    /// <summary>The JSON-RPC endpoint, the same path <c>Polhem.Northwind.Server</c> maps.</summary>
    public const string ApiPath = "/api";

    private const string BundleKey = "Northwind.BrowserBundle";

    private const string BlazorWasmBundleKey = "Northwind.BlazorWasmBundle";

    private const string FluentWasmBundleKey = "Northwind.FluentWasmBundle";

    private const string MasterKeyVariable = "POLHEM_MASTER_KEY";

    // An AES-CBC-HMAC combined key: 32 bytes of AES key and 32 of HMAC key.
    private const int MasterKeyLength = 64;

    // JSON-RPC bodies are a form's data set at most, compressed; anything larger is not this site's traffic.
    private const long MaxApiRequestBodySize = 1024 * 1024;

    /// <summary>
    /// Gives the process a random master key when none is configured.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: call it before <c>AddNorthwindBackend</c>, which otherwise sets the demo key published in
    /// <c>NorthwindCredentials.DemoMasterKey</c>. With that key and an access token, anyone could derive a session's
    /// payload key. The key only has to stay the same while sessions outlive a restart, and on the hosted site they do
    /// not: the database is created again with every container (see <c>maintainers/northwind-demo-hosting.md</c>).
    /// A key supplied in the environment is kept.
    /// </remarks>
    public static void EnsureMasterKey()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(MasterKeyVariable)))
        {
            Environment.SetEnvironmentVariable(
                MasterKeyVariable, Convert.ToBase64String(RandomNumberGenerator.GetBytes(MasterKeyLength)));
        }
    }

    /// <summary>
    /// Registers the JSON-RPC server and the published WASM bundles.
    /// </summary>
    /// <param name="builder">
    /// The web application builder, on which the Northwind backend is already registered: some registrations here
    /// replace what the backend set up.
    /// </param>
    /// <remarks>
    /// The bundle directories are <c>Northwind:BrowserBundle</c>, <c>Northwind:BlazorWasmBundle</c> and
    /// <c>Northwind:FluentWasmBundle</c>, resolved against the content root, and default to <c>browser</c>,
    /// <c>blazor-wasm</c> and <c>fluent-wasm</c> under it. Each is the <c>wwwroot</c> folder that <c>dotnet publish</c>
    /// writes for its head. A missing bundle stops the host here rather than turning every request for it into a 404.
    /// </remarks>
    public static void AddNorthwindDemo(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string bundle = ResolveBundle(builder, "BrowserBundle", "browser", "Polhem.Northwind.Browser");
        string blazorWasmBundle = ResolveBundle(builder, "BlazorWasmBundle", "blazor-wasm", "Polhem.Northwind.BlazorWasm");
        string fluentWasmBundle = ResolveBundle(builder, "FluentWasmBundle", "fluent-wasm",
            "Polhem.Northwind.BlazorFluentWasm");

        // `dotnet run` serves the framework's and the packages' static files (`_framework/blazor.web.js`,
        // `_content/...`) from the build output only when static web assets are on, which the host does by itself
        // in Development alone. A published app has the files on disk and no manifest, so this does nothing there.
        builder.WebHost.UseStaticWebAssets();

        // Registered through a factory so the container disposes the provider with the host.
        builder.Services.AddKeyedSingleton<IFileProvider>(BundleKey, (_, _) => new PhysicalFileProvider(bundle));
        builder.Services.AddKeyedSingleton<IFileProvider>(BlazorWasmBundleKey, (_, _) => new PhysicalFileProvider(blazorWasmBundle));
        builder.Services.AddKeyedSingleton<IFileProvider>(FluentWasmBundleKey, (_, _) => new PhysicalFileProvider(fluentWasmBundle));

        // The rate limit counts HTTP requests. It holds because the framework refuses JSON-RPC batches by default;
        // turning them on here would let one request carry many calls.
        builder.Services.AddJsonRpcServer(configureHttp: http => http.MaxRequestBodySize = MaxApiRequestBodySize);
        builder.Services.AddPolhemApiKeyGateCheck();

        // Northwind's SystemSettings.xml turns debug mode on for the development hosts, and in debug mode an error
        // response carries the exception's own message. A public site answers with the fixed messages instead.
        // The payload encryptor is aes-cbc-hmac, so the debug flag the backend already registered with the payload,
        // which only permits the `none` encryptor, changes nothing here.
        SysInfo.IsDebugMode = false;
        builder.Services.Replace(ServiceDescriptor.Singleton<ILoginAttemptTracker, NoLockoutLoginAttemptTracker>());

        AddForwardedClientAddress(builder.Services);
        AddRateLimits(builder.Services);
    }

    /// <summary>
    /// Takes the client address from the <c>CF-Connecting-IP</c> header of the Cloudflare edge in front of Render.
    /// </summary>
    /// <remarks>
    /// Every connection comes from Render's local proxy, so the rate limits would otherwise count all visitors as one.
    /// <c>X-Forwarded-For</c> does not name the client reliably. Measured on Render on 2026-10-30, it arrives as
    /// client, Cloudflare edge, Render internal address: the last entry is not the client, and a client can put
    /// anything first, because the proxies append to what it sent. Cloudflare writes <c>CF-Connecting-IP</c> itself
    /// and refused the requests that supplied their own. The default known proxies stay in place: Render's proxy
    /// connects from loopback, and a connection from anywhere else, such as a container published directly with
    /// <c>docker run</c>, keeps its own address whatever header it sends. See
    /// <c>maintainers/northwind-demo-hosting.md</c> for what this relies on.
    /// </remarks>
    private static void AddForwardedClientAddress(IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardedForHeaderName = "CF-Connecting-IP";
            options.ForwardLimit = 1;
        });
    }

    /// <summary>
    /// Limits each client address on the JSON-RPC endpoint and on opening a Blazor Server connection.
    /// </summary>
    /// <remarks>
    /// Static files stay unlimited: the WASM head alone requests a couple of hundred of them on its first load.
    /// The JSON-RPC limit leaves room for the burst the WASM head sends while it signs in and opens a form, and slows
    /// a script that writes through <c>/api</c>. It does not cover what a Blazor connection does once it is open:
    /// those calls run in process, over the connection's WebSocket, and only the connection count is limited.
    /// Visitors behind one address, such as an office network, share both limits.
    /// </remarks>
    private static void AddRateLimits(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                string client = GetRateLimitClient(context.Connection.RemoteIpAddress);
                if (context.Request.Path.StartsWithSegments(ApiPath))
                {
                    return RateLimitPartition.GetTokenBucketLimiter("api:" + client, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 60,
                        TokensPerPeriod = 10,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(5),
                        QueueLimit = 0,
                    });
                }
                if (IsBlazorConnectionStart(context))
                {
                    return RateLimitPartition.GetFixedWindowLimiter("circuit:" + client, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 40,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
                }
                return RateLimitPartition.GetNoLimiter(string.Empty);
            });
        });
    }

    /// <summary>
    /// Names the client a rate limit counts: the address, or for IPv6 its /64 network.
    /// </summary>
    /// <remarks>
    /// One IPv6 subscriber normally holds a whole /64, so counting single addresses would hand them a fresh limit per
    /// request.
    /// </remarks>
    private static string GetRateLimitClient(IPAddress? address)
    {
        if (address is null)
            return string.Empty;
        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4().ToString();
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        byte[] network = address.GetAddressBytes();
        Array.Clear(network, 8, 8);
        return new IPAddress(network) + "/64";
    }

    /// <summary>
    /// Whether the request opens a Blazor Server connection: the negotiation, or a WebSocket a client opens directly
    /// by skipping it. A normal visitor makes one of each per connection; the requests of an open connection are
    /// neither.
    /// </summary>
    /// <remarks>
    /// The upgrade is read from the server's own features. <c>HttpContext.WebSockets.IsWebSocketRequest</c> would say
    /// false here: the rate limiter runs before the WebSocket middleware that answers it.
    /// </remarks>
    private static bool IsBlazorConnectionStart(HttpContext context)
    {
        // The Fluent UI head is mounted under its own path, and so is its hub.
        var path = context.Request.Path;
        if (path.StartsWithSegments(NorthwindDemoSite.FluentServerPath, out var remaining))
            path = remaining;
        if (path.StartsWithSegments("/_blazor/negotiate"))
            return true;
        if (!path.StartsWithSegments("/_blazor"))
            return false;
        return context.Features.Get<IHttpUpgradeFeature>()?.IsUpgradableRequest == true
            || context.Features.Get<IHttpExtendedConnectFeature>()?.IsExtendedConnect == true;
    }

    private static string ResolveBundle(WebApplicationBuilder builder, string settingName, string defaultFolder,
        string project)
    {
        string bundle = Path.GetFullPath(
            builder.Configuration[$"Northwind:{settingName}"] ?? defaultFolder, builder.Environment.ContentRootPath);
        if (!File.Exists(Path.Combine(bundle, "index.html")))
        {
            throw new InvalidOperationException(
                $"The WASM bundle was not found in '{bundle}'. Publish {project} and point " +
                $"Northwind:{settingName} at the wwwroot folder of the publish output.");
        }
        return bundle;
    }

    /// <summary>
    /// Serves the WASM bundles under <see cref="NorthwindDemoSite.BrowserPath"/>,
    /// <see cref="NorthwindDemoSite.BlazorWasmPath"/> and <see cref="NorthwindDemoSite.FluentWasmPath"/> and maps the
    /// JSON-RPC endpoint at
    /// <see cref="ApiPath"/>.
    /// </summary>
    /// <param name="app">The built web application, after the Northwind backend has been seeded.</param>
    /// <remarks>
    /// Call it before any other middleware the host adds: the forwarded client address and the rate limits have to
    /// apply to everything after them, and the bundle has a <c>_framework/</c> folder of its own that only its path
    /// prefix keeps apart from the Blazor one.
    /// </remarks>
    public static void UseNorthwindDemo(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        EnsureNoDeploymentAdministrator(app.Services.GetRequiredService<IDbAccessFactory>());

        app.UseForwardedHeaders();
        app.UseRateLimiter();

        app.UseBrowserBundle(NorthwindDemoSite.BrowserPath, app.Services.GetRequiredKeyedService<IFileProvider>(BundleKey));
        app.UseBrowserBundle(NorthwindDemoSite.BlazorWasmPath,
            app.Services.GetRequiredKeyedService<IFileProvider>(BlazorWasmBundleKey), hasClientRoutes: true);
        app.UseBrowserBundle(NorthwindDemoSite.FluentWasmPath,
            app.Services.GetRequiredKeyedService<IFileProvider>(FluentWasmBundleKey), hasClientRoutes: true);
        app.MapJsonRpc(ApiPath);
    }

    /// <summary>
    /// Refuses to start the public site while any account is a deployment administrator.
    /// </summary>
    /// <param name="dbAccessFactory">Opens the common database, which holds <c>st_user</c>.</param>
    /// <remarks>
    /// IMPORTANT: every account of this site has a published password. A deployment administrator may manage the
    /// installation's API keys from <c>/api</c>, so one seeded by mistake would hand that to every visitor.
    /// </remarks>
    private static void EnsureNoDeploymentAdministrator(IDbAccessFactory dbAccessFactory)
    {
        var count = dbAccessFactory.Create(DbCategoryIds.Common)
            .ExecuteScalar("SELECT COUNT(*) FROM st_user WHERE deployment_admin = {0}", true);
        if (ValueUtilities.CInt(count ?? 0) != 0)
        {
            throw new InvalidOperationException(
                "The demo site publishes the password of every account, so no account may be a deployment administrator.");
        }
    }
}
