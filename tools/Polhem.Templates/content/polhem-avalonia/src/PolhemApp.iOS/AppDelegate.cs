using Avalonia;
using Avalonia.iOS;
using Foundation;
using PolhemApp.UI;

namespace PolhemApp.iOS;

/// <summary>
/// Hosts <see cref="App"/> as the single view of the iOS application. The simulator shares the host's network, so the
/// server's localhost endpoint works as it is.
/// </summary>
[Register("AppDelegate")]
#pragma warning disable CA1711 // "Delegate" is the iOS naming convention for the application delegate.
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
{
    /// <inheritdoc/>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}
