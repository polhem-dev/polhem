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
        Assert.Equal(expected, ShellLanguage.ResolveInitial(null, remembered, null, CultureInfo.GetCultureInfo(system)));
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
        Assert.Equal(expected, ShellLanguage.ResolveInitial(null, remembered, null, CultureInfo.GetCultureInfo(system)));
    }

    [Theory]
    [DisplayName("ResolveInitial falls back to English for a system language the shell does not offer")]
    [InlineData("fr-FR")]
    [InlineData("zh-CN")]
    [InlineData("ja-JP")]
    public void ResolveInitial_UnofferedSystem_FallsBackToEnglish(string system)
    {
        Assert.Equal(ShellLanguage.English, ShellLanguage.ResolveInitial(null, null, null, CultureInfo.GetCultureInfo(system)));
    }

    [Fact]
    [DisplayName("ResolveInitial falls back to English for the invariant culture")]
    public void ResolveInitial_Invariant_FallsBackToEnglish()
    {
        Assert.Equal(ShellLanguage.English, ShellLanguage.ResolveInitial(string.Empty, string.Empty, null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [DisplayName("ResolveInitial prefers the language requested for the run over the remembered one")]
    [InlineData("en", "zh-TW", "zh-TW", "en")]
    [InlineData("zh-tw", "en", "en-US", "zh-TW")]
    [InlineData("fr", "zh-TW", "en-US", "zh-TW")]
    [InlineData("", "zh-TW", "en-US", "zh-TW")]
    public void ResolveInitial_Requested_WinsOverRemembered(string requested, string remembered, string system, string expected)
    {
        Assert.Equal(expected, ShellLanguage.ResolveInitial(requested, remembered, null, CultureInfo.GetCultureInfo(system)));
    }

    [Theory]
    [DisplayName("ResolveInitial uses the application's default language before the system language")]
    [InlineData("", "en-US", "zh-TW", "en")]
    [InlineData("", "zh-TW", "en-US", "zh-TW")]
    [InlineData("zh-TW", "en-US", "en-US", "zh-TW")]
    [InlineData("", "fr-FR", "zh-TW", "zh-TW")]
    [InlineData("", "not-a-culture", "zh-TW", "zh-TW")]
    public void ResolveInitial_Configured_WinsOverSystem(string remembered, string configured, string system, string expected)
    {
        Assert.Equal(expected, ShellLanguage.ResolveInitial(null, remembered, configured, CultureInfo.GetCultureInfo(system)));
    }

    [Theory]
    [DisplayName("FindArgument reads Language= from the command line after the program and ignores other arguments")]
    [InlineData(new[] { "app", "Language=en" }, "en")]
    [InlineData(new[] { "app", "Endpoint=http://x", "language= zh-TW " }, "zh-TW")]
    [InlineData(new[] { "app", "Language=en", "Language=zh-TW" }, "zh-TW")]
    [InlineData(new[] { "Language=en" }, "")]
    [InlineData(new[] { "app", "Lang=en" }, "")]
    public void FindArgument_ReadsLanguageArgument(string[] args, string expected)
    {
        Assert.Equal(expected, ShellLanguage.FindArgument(args));
    }
}
