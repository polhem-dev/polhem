using System.Globalization;
using Avalonia;
using Avalonia.Browser;
using Polhem.Api.Client;
using Polhem.Northwind.Browser.Storage;
using Polhem.Northwind.UI;
using Polhem.UI.Core;

internal sealed partial class Program
{
    /// <summary>
    /// Browser (WASM) head entry point. Wires the Polhem client-side singletons before any
    /// Avalonia control runs, then starts the Avalonia browser app against the
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

        // Same client contract as the desktop head (Remote connector to the JSON-RPC
        // backend), but the browser has no persistent file system, so the endpoint and the
        // API key go through localStorage instead of the default `FileEndpointStorage`.
        ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
        var storage = new BrowserLocalStorageEndpointStorage("Polhem.Northwind");
        ClientInfo.EndpointStorage = storage;
        ClientInfo.ApiKeyStorage = storage;
        // The shipped key only seeds empty storage on first run; after that the stored value wins,
        // so swapping keys is a settings change rather than a rebuild.
        ClientInfo.ApplyApiKey(AppDefaults.ApiKey);

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
