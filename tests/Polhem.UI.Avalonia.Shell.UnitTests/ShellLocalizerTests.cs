using System.ComponentModel;
using System.Globalization;
using Polhem.UI.Avalonia.Shell.Localization;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The shell's own text source. Each test uses its own instance rather than the process-wide
/// <see cref="ShellLocalizer.Current"/>.
/// </summary>
public class ShellLocalizerTests
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo(ShellLanguage.English);
    private static readonly CultureInfo s_chinese = CultureInfo.GetCultureInfo(ShellLanguage.TraditionalChinese);

    [Fact]
    [DisplayName("A new localizer returns English text")]
    public void Indexer_NewLocalizer_ReturnsEnglish()
    {
        var localizer = new ShellLocalizer();

        Assert.Equal(ShellLanguage.English, localizer.Culture.Name);
        Assert.Equal("Connect", localizer["Connection_Connect"]);
    }

    [Fact]
    [DisplayName("A key no resource defines shows the key itself, so the gap is visible")]
    public void Indexer_MissingKey_ReturnsKey()
    {
        var localizer = new ShellLocalizer { Culture = s_chinese };

        Assert.Equal("No_Such_Key", localizer["No_Such_Key"]);
    }

    [Fact]
    [DisplayName("An empty key returns an empty string")]
    public void Indexer_EmptyKey_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, new ShellLocalizer()[string.Empty]);
    }

    [Fact]
    [DisplayName("Changing the culture returns the text in the new language and raises CultureChanged once")]
    public void Culture_Changed_ReturnsTranslationAndRaisesOnce()
    {
        var localizer = new ShellLocalizer();
        var raised = 0;
        localizer.CultureChanged += (_, _) => raised++;

        localizer.Culture = s_chinese;

        Assert.Equal(1, raised);
        Assert.NotEqual(ShellLocalizer.Get("Connection_Connect", s_english), localizer["Connection_Connect"]);
    }

    [Fact]
    [DisplayName("Setting the same language again, in any letter case, raises no CultureChanged")]
    public void Culture_SameLanguage_DoesNotRaise()
    {
        var localizer = new ShellLocalizer { Culture = s_chinese };
        var raised = 0;
        localizer.CultureChanged += (_, _) => raised++;

        localizer.Culture = CultureInfo.GetCultureInfo("zh-tw");

        Assert.Equal(0, raised);
    }

    [Fact]
    [DisplayName("Setting a null culture throws")]
    public void Culture_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ShellLocalizer().Culture = null!);
    }

    [Fact]
    [DisplayName("Format fills the arguments into the text of the current language")]
    public void Format_FillsArguments()
    {
        var localizer = new ShellLocalizer();

        var english = localizer.Format("Status_ConnectFailed", "timeout");
        localizer.Culture = s_chinese;
        var chinese = localizer.Format("Status_ConnectFailed", "timeout");

        Assert.Equal("Connection failed: timeout", english);
        Assert.Contains("timeout", chinese, StringComparison.Ordinal);
        Assert.NotEqual(english, chinese);
    }
}
