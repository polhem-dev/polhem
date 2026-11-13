using System.ComponentModel;
using Avalonia.Styling;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public class PolhemShellOptionsTests
{
    [Fact]
    [DisplayName("DefaultThemeVariant starts light when the application sets none")]
    public void DefaultThemeVariant_NotSet_IsLight()
    {
        Assert.Equal(ThemeVariant.Light, new PolhemShellOptions().DefaultThemeVariant);
    }

    [Fact]
    [DisplayName("DefaultThemeVariant keeps the variant the application sets")]
    public void DefaultThemeVariant_SetDark_ReturnsDark()
    {
        var options = new PolhemShellOptions { DefaultThemeVariant = ThemeVariant.Dark };

        Assert.Equal(ThemeVariant.Dark, options.DefaultThemeVariant);
    }
}
