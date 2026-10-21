using Avalonia;
using Avalonia.iOS;
using Polhem.Northwind.UI;
using Foundation;

namespace Polhem.Northwind.iOS;

/// <summary>
/// iOS application delegate. <see cref="AvaloniaAppDelegate{TApp}"/> hosts the shared <see cref="NorthwindApp"/>
/// from <c>Polhem.Northwind.UI</c> as the single view (iOS has no native window). The shell configures the client;
/// its default storage writes under the local application data folder, inside the app's writable sandbox container.
/// </summary>
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix — "Delegate" is the iOS convention.
public partial class AppDelegate : AvaloniaAppDelegate<NorthwindApp>
#pragma warning restore CA1711
{
    /// <inheritdoc/>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
