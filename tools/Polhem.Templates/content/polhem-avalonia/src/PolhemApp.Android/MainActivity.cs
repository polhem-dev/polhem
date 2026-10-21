using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace PolhemApp.Android;

/// <summary>
/// The launcher activity. It hosts the view <see cref="Application"/> builds; handling rotation itself lets Avalonia
/// reflow the layout instead of the activity being recreated.
/// </summary>
[Activity(
    Label = "PolhemApp",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
