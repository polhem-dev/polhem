using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Polhem.DefineEditor.BrowserPreview;

/// <summary>
/// The in-process web server behind the browser preview: Kestrel on the loopback address, serving one Blazor Server
/// page that renders a FormLayout with the framework's <see cref="Polhem.Web.Blazor.Server.Controls.FormLayoutPresenter"/>.
/// </summary>
/// <remarks>
/// <para>
/// The server starts on the first request for a preview URL and stops when the host is disposed. It listens on
/// 127.0.0.1 on a port the system picks, and every page URL carries a random token, so another process on the machine
/// (or a web page that rebinds a host name to 127.0.0.1) cannot read the layouts being edited.
/// </para>
/// <para>
/// Starting fails with <see cref="InvalidOperationException"/> when the static web asset manifest is missing next to
/// the executable, and with <see cref="IOException"/> when Kestrel cannot bind. The editor reports both and keeps
/// running.
/// </para>
/// </remarks>
public sealed class BrowserPreviewHost : IAsyncDisposable
{
    internal const string PreviewPathPrefix = "/preview/";

    private static readonly Lazy<BrowserPreviewHost> s_shared =
        new(() => new BrowserPreviewHost(new BrowserPreviewHub(), static () => Services.LocalizationService.Current.Culture));

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<CultureInfo> _culture;
    private readonly string _token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private WebApplication? _app;
    private Uri? _baseAddress;

    /// <summary>
    /// Initializes a host that serves the snapshots of <paramref name="hub"/>.
    /// </summary>
    /// <param name="hub">The snapshots to serve.</param>
    /// <param name="culture">Returns the culture the page renders in, read when a browser connects.</param>
    public BrowserPreviewHost(BrowserPreviewHub hub, Func<CultureInfo> culture)
    {
        Hub = hub;
        _culture = culture;
    }

    /// <summary>
    /// Gets the host the editor uses, created on first use.
    /// </summary>
    public static BrowserPreviewHost Shared => s_shared.Value;

    /// <summary>
    /// Gets whether <see cref="Shared"/> was created, so the editor can stop it on exit without starting it.
    /// </summary>
    public static bool IsSharedCreated => s_shared.IsValueCreated;

    /// <summary>
    /// Gets the snapshots this host serves.
    /// </summary>
    public BrowserPreviewHub Hub { get; }

    /// <summary>
    /// Gets whether the server is running; the editor publishes snapshots only then.
    /// </summary>
    public bool IsRunning => _app is not null;

    /// <summary>
    /// Returns the preview URL of a document, starting the server if it is not running.
    /// </summary>
    /// <param name="documentId">The id from <see cref="BrowserPreviewHub.GetDocumentId"/>.</param>
    public async Task<Uri> GetDocumentUrlAsync(string documentId)
    {
        var baseAddress = await StartAsync().ConfigureAwait(false);
        return new Uri(baseAddress, string.Create(CultureInfo.InvariantCulture, $"{PreviewPathPrefix}{_token}/{documentId}"));
    }

    /// <summary>
    /// Starts the server if it is not running and returns its base address.
    /// </summary>
    public async Task<Uri> StartAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_app is not null && _baseAddress is not null)
                return _baseAddress;

            var app = CreateApplication();
            string address;
            try
            {
                MapEndpoints(app);
                await app.StartAsync().ConfigureAwait(false);
                address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
                    ?? throw new InvalidOperationException("The browser preview server started without an address.");
            }
            catch (InvalidOperationException)
            {
                await app.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            catch (IOException)
            {
                await app.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            _baseAddress = new Uri(address);
            _app = app;
            return _baseAddress;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_app is null) return;
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
            _baseAddress = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // The entry assembly is the test host under a test run, and MapStaticAssets names its manifest after
            // the application, so name it after this assembly.
            ApplicationName = typeof(BrowserPreviewHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        });
        // Serves the assets from their source folders when the editor runs from its build output (dotnet run);
        // a published editor has them in wwwroot instead, and this finds nothing to add.
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        // NOTE: the page posts no form, yet rendering still reads the antiforgery cookie the browser holds for
        // 127.0.0.1, whatever the port. A cookie from an earlier run of the editor was protected with that run's
        // in-memory keys, so every new run would log a failure to decrypt it.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Antiforgery", LogLevel.None);
        // NOTE: the default key ring is written to the user's profile. The keys that protect the circuit only have
        // to live as long as this process, so keep them in memory.
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton(Hub);
        return builder.Build();
    }

    private void MapEndpoints(WebApplication app)
    {
        app.Use(RejectWithoutToken);
        app.Use(ApplyEditorCulture);
        app.MapStaticAssets();
        // The page posts no form, so it needs no antiforgery check.
        app.MapRazorComponents<BrowserPreviewApp>().AddInteractiveServerRenderMode().DisableAntiforgery();
    }

    private Task RejectWithoutToken(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith(PreviewPathPrefix, StringComparison.Ordinal) && !HasToken(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }
        return next(context);
    }

    private bool HasToken(string path)
    {
        var rest = path.AsSpan(PreviewPathPrefix.Length);
        var end = rest.IndexOf('/');
        var token = end < 0 ? rest : rest[..end];
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(token.ToString()), Encoding.ASCII.GetBytes(_token));
    }

    // A circuit keeps the culture of the request that opened it, so the page follows the editor's language as it was
    // when the browser connected; a reload picks up a later switch.
    private Task ApplyEditorCulture(HttpContext context, RequestDelegate next)
    {
        var culture = _culture();
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return next(context);
    }
}
