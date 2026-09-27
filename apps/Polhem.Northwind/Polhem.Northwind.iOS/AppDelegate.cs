using Avalonia;
using Avalonia.iOS;
using Polhem.Api.Client;
using Polhem.Northwind.UI;
using Polhem.UI.Core;
using Foundation;

namespace Polhem.Northwind.iOS;

/// <summary>
/// iOS application delegate. Wires the Polhem client-side singletons before any Avalonia control
/// runs — the same client contract as the desktop / browser heads — then lets
/// <see cref="AvaloniaAppDelegate{TApp}"/> host the shared <see cref="App"/> from
/// <c>Polhem.Northwind.UI</c> as the single view (iOS has no native window).
/// </summary>
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix — "Delegate" is the iOS convention.
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
{
    /// <inheritdoc/>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Configure the Polhem client singletons before the Avalonia app initialises. The endpoint
        // and the API key keep their default storage: it writes under the local application data
        // folder, which on iOS is inside the app's writable sandbox container.
        ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
        // The shipped key only seeds empty storage on first run; after that the stored value wins,
        // so swapping keys is a settings change rather than a rebuild.
        ClientInfo.ApplyApiKey(AppDefaults.ApiKey);

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
