using Avalonia;
using Polhem.Api.Client;
using Polhem.Northwind.UI;
using Polhem.UI.Core;

namespace Polhem.Northwind.Desktop;

/// <summary>
/// Desktop head — the thin process entry point. Wires the Polhem client-side singletons
/// (<see cref="ApiClientInfo"/> + <see cref="ClientInfo"/>) before any
/// Avalonia control instantiates, then hands control to the classic-desktop lifetime
/// hosting the shared <see cref="App"/> from <c>Polhem.Northwind.UI</c>.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Application entry point. <c>STAThread</c> is required by Windows for Avalonia to
    /// drive native dialogs / drag-drop / clipboard.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure the Polhem client singletons before any control or VM runs. The endpoint and
        // the API key keep their default storage, a per-user folder under the local application
        // data directory.
        ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
        // The shipped key only seeds empty storage on first run; after that the stored value wins,
        // so swapping keys is a settings change rather than a rebuild.
        ClientInfo.ApplyApiKey(AppDefaults.ApiKey);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Builds the Avalonia <see cref="AppBuilder"/>. Kept as a separate method so the
    /// previewer / visual-tree tooling can reuse the same setup.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
