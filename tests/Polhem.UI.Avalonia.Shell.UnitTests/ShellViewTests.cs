using System.ComponentModel;
using Avalonia;
using Polhem.UI.Avalonia.Shell.Views;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The root padding <see cref="ShellView"/> keeps on phones: the safe area, raised above the on-screen keyboard.
/// </summary>
public class ShellViewTests
{
    private static readonly Thickness s_safeArea = new(0, 47, 0, 34);

    [Fact]
    [DisplayName("With the keyboard closed the root padding is the safe area")]
    public void ComputePadding_KeyboardClosed_ReturnsSafeArea()
    {
        Assert.Equal(s_safeArea, ShellView.ComputePadding(s_safeArea, default, 852));
    }

    [Fact]
    [DisplayName("An open keyboard raises the bottom padding to the height it covers")]
    public void ComputePadding_KeyboardOpen_ClearsKeyboard()
    {
        var keyboard = new Rect(0, 516, 393, 336);

        Assert.Equal(new Thickness(0, 47, 0, 336), ShellView.ComputePadding(s_safeArea, keyboard, 852));
    }

    [Fact]
    [DisplayName("A keyboard lower than the safe area leaves the safe-area bottom in place")]
    public void ComputePadding_KeyboardBelowSafeArea_KeepsSafeArea()
    {
        var keyboard = new Rect(0, 832, 393, 20);

        Assert.Equal(s_safeArea, ShellView.ComputePadding(s_safeArea, keyboard, 852));
    }
}
