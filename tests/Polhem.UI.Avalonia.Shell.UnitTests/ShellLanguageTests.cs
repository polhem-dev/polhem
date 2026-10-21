using System.ComponentModel;
using System.Globalization;
using Polhem.UI.Avalonia.Shell.Localization;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public class ShellLanguageTests
{
    [Theory]
    [DisplayName("ResolveInitial prefers the remembered language over the system language")]
    [InlineData("zh-TW", "en-US", "zh-TW")]
    [InlineData("en", "zh-TW", "en")]
    [InlineData("ZH-tw", "en-US", "zh-TW")]
    public void ResolveInitial_Remembered_Wins(string remembered, string system, string expected)
    {
        Assert.Equal(expected, ShellLanguage.ResolveInitial(remembered, CultureInfo.GetCultureInfo(system)));
    }

    [Theory]
    [DisplayName("ResolveInitial maps the system language when nothing usable is remembered")]
    [InlineData("", "zh-TW", "zh-TW")]
    [InlineData("", "zh-Hant-TW", "zh-TW")]
    [InlineData("", "zh-HK", "zh-TW")]
    [InlineData("", "en-GB", "en")]
    [InlineData("fr", "zh-TW", "zh-TW")]
    public void ResolveInitial_NoRemembered_MapsSystem(string remembered, string system, string expected)
    {
        Assert.Equal(expected, ShellLanguage.ResolveInitial(remembered, CultureInfo.GetCultureInfo(system)));
    }

    [Theory]
    [DisplayName("ResolveInitial falls back to English for a system language the shell does not offer")]
    [InlineData("fr-FR")]
    [InlineData("zh-CN")]
    [InlineData("ja-JP")]
    public void ResolveInitial_UnofferedSystem_FallsBackToEnglish(string system)
    {
        Assert.Equal(ShellLanguage.English, ShellLanguage.ResolveInitial(null, CultureInfo.GetCultureInfo(system)));
    }

    [Fact]
    [DisplayName("ResolveInitial falls back to English for the invariant culture")]
    public void ResolveInitial_Invariant_FallsBackToEnglish()
    {
        Assert.Equal(ShellLanguage.English, ShellLanguage.ResolveInitial(string.Empty, CultureInfo.InvariantCulture));
    }
}
