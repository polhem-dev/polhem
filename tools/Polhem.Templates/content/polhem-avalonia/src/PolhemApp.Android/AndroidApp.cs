using Polhem.UI.Avalonia.Shell;
using PolhemApp.UI;

namespace PolhemApp.Android;

/// <summary>
/// <see cref="App"/> as the Android emulator reaches the server: the emulator's own localhost is the emulator, and
/// 10.0.2.2 is the loopback of the machine that runs it.
/// </summary>
public class AndroidApp : App
{
    /// <inheritdoc/>
    protected override ShellOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.DefaultEndpoint = "http://10.0.2.2:5180/api";
        return options;
    }
}
