using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Polhem.UI.Avalonia.Shell.Localization;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

public class SemiLocaleTests
{
    private const string YearKey = "STRING_DATEPICKER_YEAR_TEXT";

    [Theory]
    [DisplayName("Apply puts the date picker placeholders in English for an English culture Semi does not ship")]
    [InlineData("en")]
    [InlineData("en-TW")]
    [InlineData("en-US")]
    public void Apply_EnglishCulture_UsesEnglishText(string culture)
    {
        var application = new Application();

        SemiLocale.Apply(application, CultureInfo.GetCultureInfo(culture));

        Assert.Equal("year", application.Resources[YearKey]);
    }

    [Fact]
    [DisplayName("Apply puts the date picker placeholders in Traditional Chinese for zh-TW")]
    public void Apply_TraditionalChinese_UsesChineseText()
    {
        var application = new Application();

        SemiLocale.Apply(application, CultureInfo.GetCultureInfo("zh-TW"));

        Assert.Equal("年", application.Resources[YearKey]);
    }

    [Fact]
    [DisplayName("Apply switches the text in place when the language changes back to English")]
    public void Apply_SwitchBack_ReplacesText()
    {
        var application = new Application();
        SemiLocale.Apply(application, CultureInfo.GetCultureInfo("zh-TW"));

        SemiLocale.Apply(application, CultureInfo.GetCultureInfo("en"));

        Assert.Equal("year", application.Resources[YearKey]);
    }

    [Fact]
    [DisplayName("Apply falls back to English for a language Semi does not ship")]
    public void Apply_UnshippedLanguage_FallsBackToEnglish()
    {
        var application = new Application();

        SemiLocale.Apply(application, CultureInfo.GetCultureInfo("th-TH"));

        Assert.Equal("year", application.Resources[YearKey]);
    }

    [Fact]
    [DisplayName("Candidates start with the fallback and end with the culture itself")]
    public void Candidates_SpecificCulture_EndsWithCulture()
    {
        var culture = CultureInfo.GetCultureInfo("en-TW");

        var candidates = SemiLocale.Candidates(culture).Select(c => c.Name).ToList();

        Assert.Equal(["en-US", "en-US", "en", "en-TW", "en-TW"], candidates);
    }
}
