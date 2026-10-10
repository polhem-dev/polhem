using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Polhem.Api.Client;
using Polhem.Northwind.UI;
using Polhem.UI.Core;

namespace Polhem.Northwind.Android;

/// <summary>
/// Android application object — the analog of the iOS <c>AppDelegate</c>. Android instantiates
/// it once at process start, before any activity, and <see cref="AvaloniaAndroidApplication{TApp}"/>
/// builds the single-view Avalonia lifetime here (the <see cref="MainActivity"/> only hosts the
/// resulting view). This is therefore the place to wire the Polhem client-side singletons — the same
/// client contract as the desktop / browser / iOS heads — before any Avalonia control runs.
/// </summary>
[Application]
public class Application : AvaloniaAndroidApplication<App>
{
    /// <summary>
    /// Required JNI bridge constructor. The Android runtime calls this when it materialises the
    /// managed peer for the native application instance.
    /// </summary>
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc/>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Configure the Polhem client singletons before the Avalonia app initialises. The endpoint
        // and the API key keep their default storage: it writes under the local application data
        // folder, which on Android is inside the app's private data directory.
        ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
        // The shipped key only seeds empty storage on first run; after that the stored value wins,
        // so swapping keys is a settings change rather than a rebuild.
        ClientInfo.ApplyApiKey(AppDefaults.ApiKey);

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
