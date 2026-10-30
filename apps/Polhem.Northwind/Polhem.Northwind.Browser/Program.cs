using System.Globalization;
using Avalonia;
using Avalonia.Browser;
using Polhem.Northwind.UI;
using Polhem.UI.ApiBridge;
using Polhem.UI.Avalonia.Shell.Browser;

internal sealed partial class Program
{
    /// <summary>
    /// Browser (WASM) head entry point. Replaces the client's storage before any Avalonia control
    /// runs, then starts the Avalonia browser app against the
    /// <c>&lt;div id="out"&gt;</c> mount point in <c>wwwroot/index.html</c>.
    /// </summary>
    private static Task Main(string[] args)
    {
        // Starts the head in English whatever the browser's language is.
        //
        // In the browser the process culture comes from the browser language, and it holds until
        // sign-in, when ClientInfo.ApplyLoginResult replaces it with the account's culture
        // (st_user.culture, or the deployment's default language when that is empty). So this pin
        // only covers the screens before sign-in.
        //
        // That includes the demo-tw account, whose culture is zh-TW: this head then loads the zh-TW
        // resources, and Inter, the bundled font, carries no CJK glyphs. The other heads borrow
        // PingFang or Noto CJK from the operating system when a glyph is missing; the browser
        // sandbox has no system font to borrow, so those labels render as tofu boxes. Shipping a
        // CJK font would fix it, and the smallest usable one costs 5.4 MB, so the demo documents
        // the gap instead: use the demo account in this head.
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

        // The browser has no persistent file system, so the endpoint, the API key and the shell's preferences go
        // through localStorage instead of the default files. The language switch stays off: the bundled font has no
        // CJK glyphs (see above).
        var storage = new BrowserLocalStorage("Polhem.Northwind");
        ClientInfo.EndpointStorage = storage;
        ClientInfo.ApiKeyStorage = storage;

        return AppBuilder.Configure(() => new NorthwindApp
            {
                ConfigureOptions = options =>
                {
                    options.DefaultEndpoint = ResolveDefaultEndpoint(args, options.DefaultEndpoint);
                    // demo-tw is left out of the hint: its captions render as empty boxes here (see above).
                    options.SignInHint = "Use demo / demo, or demo-ro / demo for the read-only account.";
                    if (IsDemoSite(args))
                    {
                        // The public site leaves the account to the visitor, so the read-only one is a real
                        // choice rather than something nobody tries.
                        options.DefaultUserId = string.Empty;
                        options.DefaultPassword = string.Empty;
                    }
                    options.PreferenceStorage = storage;
                    options.AllowLanguageSwitch = false;
                },
            })
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    /// <summary>
    /// Pre-fills the endpoint of the host that served the page when that host is the demo site.
    /// </summary>
    /// <param name="args">The arguments <c>wwwroot/main.js</c> passes; the first is the page address.</param>
    /// <param name="fallback">The endpoint of the development layout, <c>Polhem.Northwind.Server</c>.</param>
    /// <remarks>
    /// The demo site (<c>Polhem.Northwind.Blazor</c> in its Demo environment) serves this bundle under <c>/web/</c>
    /// and the JSON-RPC endpoint at <c>/api</c> on the same origin. The development server serves the bundle at the
    /// root and the API runs on another port, so every other address keeps <paramref name="fallback"/>.
    /// A value the visitor saved on the connection screen still wins over either.
    /// </remarks>
    private static string ResolveDefaultEndpoint(string[] args, string fallback)
        => IsDemoSite(args) ? new Uri(new Uri(args[0]), "/api").AbsoluteUri : fallback;

    /// <summary>
    /// Whether the page was served by the demo site, which serves this bundle under <c>/web/</c>.
    /// </summary>
    /// <param name="args">The arguments <c>wwwroot/main.js</c> passes; the first is the page address.</param>
    /// <remarks>
    /// IMPORTANT: the path is the demo host's <c>NorthwindDemo.BrowserPath</c>. Change both together.
    /// </remarks>
    private static bool IsDemoSite(string[] args)
        => args.Length > 0 && Uri.TryCreate(args[0], UriKind.Absolute, out var page)
            && page.AbsolutePath.StartsWith("/web/", StringComparison.OrdinalIgnoreCase);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<NorthwindApp>();
}
