using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Polhem.Northwind.UI;

namespace Polhem.Northwind.Android;

/// <summary>
/// Android application object — the analog of the iOS <c>AppDelegate</c>. Android instantiates
/// it once at process start, before any activity, and <see cref="AvaloniaAndroidApplication{TApp}"/>
/// builds the single-view Avalonia lifetime here (the <see cref="MainActivity"/> only hosts the
/// resulting view). The shell configures the client; its default storage writes under the local application data
/// folder, inside the app's private data directory.
/// </summary>
[Application]
public class Application : AvaloniaAndroidApplication<NorthwindApp>
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
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
