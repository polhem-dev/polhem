using Avalonia;
using PolhemApp.UI;

namespace PolhemApp.Desktop;

internal static class Program
{
    // Windows needs STAThread for Avalonia's native dialogs, drag and drop and the clipboard.
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // A separate method so the Avalonia previewer can reuse the same setup.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
