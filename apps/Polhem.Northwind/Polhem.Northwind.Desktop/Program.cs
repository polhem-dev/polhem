using Avalonia;
using Polhem.Northwind.UI;

namespace Polhem.Northwind.Desktop;

/// <summary>
/// Desktop head: the process entry point. Starts <see cref="NorthwindApp"/> in a classic desktop lifetime; the shell
/// configures the client and keeps the endpoint, the API key and the preferences in a per-user folder.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Application entry point. <c>STAThread</c> is required by Windows for Avalonia to drive native dialogs,
    /// drag-drop and the clipboard.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Builds the Avalonia <see cref="AppBuilder"/>. A separate method so the previewer can reuse the same setup.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<NorthwindApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
