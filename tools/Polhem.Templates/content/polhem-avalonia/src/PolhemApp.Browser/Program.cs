using System.Globalization;
using Avalonia;
using Avalonia.Browser;
using Polhem.UI.ApiBridge;
using Polhem.UI.Avalonia.Shell.Browser;
using PolhemApp.UI;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        // Inter, the bundled font, has no CJK glyphs, and the browser has no system font to borrow them from, so the
        // screens before sign-in start in English and the language switch stays off. After sign-in the account's
        // culture applies; an account whose culture is Chinese shows empty boxes in this head.
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

        // The browser has no persistent file system: keep the endpoint, the API key and the shell's preferences in
        // localStorage.
        var storage = new BrowserLocalStorage("PolhemApp");
        ClientInfo.EndpointStorage = storage;
        ClientInfo.ApiKeyStorage = storage;

        return AppBuilder.Configure(() => new App
            {
                ConfigureOptions = options =>
                {
                    options.PreferenceStorage = storage;
                    options.AllowLanguageSwitch = false;
                },
            })
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    // A separate method so the Avalonia previewer can reuse the same setup.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
