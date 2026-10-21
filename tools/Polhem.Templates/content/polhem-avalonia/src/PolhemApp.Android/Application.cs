using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace PolhemApp.Android;

/// <summary>
/// The Android application object. Android creates it once at process start, before any activity, and it builds the
/// single-view Avalonia lifetime that <see cref="MainActivity"/> hosts.
/// </summary>
[Application]
public class Application : AvaloniaAndroidApplication<AndroidApp>
{
    /// <summary>
    /// The constructor the Android runtime calls when it creates the managed peer of the native application.
    /// </summary>
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <inheritdoc/>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}
