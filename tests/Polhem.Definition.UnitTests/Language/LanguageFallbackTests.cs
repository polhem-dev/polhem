using System.ComponentModel;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Tests for <see cref="LanguageFallback.GetChain"/>, the one fall-back chain every language
    /// lookup walks.
    /// </summary>
    public class LanguageFallbackTests
    {
        [Fact]
        [DisplayName("GetChain lists the requested culture, its parents, then the default language")]
        public void GetChain_RegionalCulture_ListsParentsThenDefault()
        {
            Assert.Equal(["fr-CA", "fr", "zh-TW"], LanguageFallback.GetChain("fr-CA", "zh-TW"));
        }

        [Theory]
        [InlineData("en-GB", new[] { "en-GB", "en" })]
        [InlineData("en", new[] { "en" })]
        [InlineData("EN-us", new[] { "EN-us", "EN" })]
        [DisplayName("GetChain stops at an English culture and leaves out the default language")]
        public void GetChain_EnglishCulture_StopsBeforeDefault(string lang, string[] expected)
        {
            Assert.Equal(expected, LanguageFallback.GetChain(lang, "zh-TW"));
        }

        [Fact]
        [DisplayName("GetChain keeps an English default language for a non-English culture")]
        public void GetChain_NonEnglishWithEnglishDefault_KeepsDefault()
        {
            Assert.Equal(["fr-FR", "fr", "en-US"], LanguageFallback.GetChain("fr-FR", "en-US"));
        }

        [Fact]
        [DisplayName("GetChain walks every subtag of a script-qualified culture")]
        public void GetChain_ScriptCulture_WalksEverySubtag()
        {
            Assert.Equal(["zh-Hant-TW", "zh-Hant", "zh", "en-US"], LanguageFallback.GetChain("zh-Hant-TW", "en-US"));
        }

        [Fact]
        [DisplayName("GetChain drops a trailing singleton together with the subtag it introduced")]
        public void GetChain_PrivateUseSubtag_DropsSingleton()
        {
            Assert.Equal(["en-x-custom", "en"], LanguageFallback.GetChain("en-x-custom", ""));
        }

        [Fact]
        [DisplayName("GetChain lists a culture once, ignoring case, when the default repeats it")]
        public void GetChain_DefaultAlreadyInChain_NotRepeated()
        {
            Assert.Equal(["zh-TW", "zh"], LanguageFallback.GetChain("zh-TW", "ZH-tw"));
            Assert.Equal(["en-US", "en"], LanguageFallback.GetChain("en-US", "en"));
        }

        [Fact]
        [DisplayName("GetChain with no requested culture starts at the default language")]
        public void GetChain_EmptyLang_StartsAtDefault()
        {
            Assert.Equal(["zh-TW"], LanguageFallback.GetChain("  ", "zh-TW"));
            Assert.Equal(["zh-TW"], LanguageFallback.GetChain(null, "zh-TW"));
        }

        [Fact]
        [DisplayName("GetChain is empty when neither a culture nor a default language is given")]
        public void GetChain_NothingGiven_IsEmpty()
        {
            Assert.Empty(LanguageFallback.GetChain("", ""));
            Assert.Empty(LanguageFallback.GetChain(null, null));
        }

        [Fact]
        [DisplayName("GetChain accepts an underscore separator the way it accepts a hyphen")]
        public void GetChain_UnderscoreSeparator_WalksParents()
        {
            Assert.Equal(["de_AT", "de"], LanguageFallback.GetChain("de_AT", ""));
        }
    }
}
