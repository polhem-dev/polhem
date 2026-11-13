using Avalonia;
using Semi.Avalonia;
using Semi.Avalonia.DataGrid;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The application the headless view tests run in: the Semi theme the shell applies, so the controls have their
/// templates, without the process-wide client set-up of <see cref="ShellApplication"/>.
/// </summary>
public sealed class HeadlessTestApplication : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        Styles.Add(new SemiTheme());
        Styles.Add(new DataGridSemiTheme());
    }
}
