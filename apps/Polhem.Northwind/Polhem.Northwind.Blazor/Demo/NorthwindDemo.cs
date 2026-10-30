using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Polhem.Core;
using Polhem.Db;
using Polhem.Definition.Database;
using Polhem.Definition.Security;
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
    /// IMPORTANT: the Browser head's <c>Program.IsDemoSite</c> recognises this path to pre-fill the same-origin
    /// endpoint and leave the account empty. Change both together.
    /// </remarks>
    public const string BrowserPath = "/web";

    private const string BundleKey = "Northwind.BrowserBundle";

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
    /// Registers the JSON-RPC server and the published WASM bundle.
    /// </summary>
    /// <param name="builder">
    /// The web application builder, on which the Northwind backend is already registered: some registrations here
    /// replace what the backend set up.
    /// </param>
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

        // The rate limit counts HTTP requests, and a batch would carry many calls in one. The Polhem clients send
        // no batches, so the demo refuses them rather than letting a batch multiply the limit.
        builder.Services.AddJsonRpcServer(
            server => server.MaxBatchSize = 0,
            http => http.MaxRequestBodySize = MaxApiRequestBodySize);
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
    /// Takes the client address from the <c>X-Forwarded-For</c> entry the hosting platform's proxy appends.
    /// </summary>
    /// <remarks>
    /// The site runs behind one reverse proxy (Render's), so every connection comes from the proxy and the rate
    /// limits would otherwise count all visitors as one. <see cref="ForwardedHeadersOptions.ForwardLimit"/> of one
    /// reads only the last entry, the one the proxy wrote; whatever a client put before it is ignored. The proxy's
    /// own address is not known in advance, hence the cleared lists. Run without a proxy, a client could name any
    /// address and choose its rate-limit partition, which is why this is the Demo environment's setting only.
    /// </remarks>
    private static void AddForwardedClientAddress(IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
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
                string client = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
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
        if (context.Request.Path.StartsWithSegments("/_blazor/negotiate"))
            return true;
        if (!context.Request.Path.StartsWithSegments("/_blazor"))
            return false;
        return context.Features.Get<IHttpUpgradeFeature>()?.IsUpgradableRequest == true
            || context.Features.Get<IHttpExtendedConnectFeature>()?.IsExtendedConnect == true;
    }

    /// <summary>
    /// Serves the WASM bundle under <see cref="BrowserPath"/> and maps the JSON-RPC endpoint at
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

        app.UseBrowserBundle(BrowserPath, app.Services.GetRequiredKeyedService<IFileProvider>(BundleKey));
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
