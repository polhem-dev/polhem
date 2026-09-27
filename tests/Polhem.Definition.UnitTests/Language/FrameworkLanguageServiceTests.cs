using System.ComponentModel;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Tests for <see cref="FrameworkLanguageService"/>: the framework's shipped translations, and a
    /// host service layered over them.
    /// </summary>
    public class FrameworkLanguageServiceTests
    {
        [Fact]
        [DisplayName("The shipped zh-TW translation of the framework's UI text resolves without any host resource")]
        public void TryResolveLangText_ShippedTranslation_Resolves()
        {
            var service = new FrameworkLanguageService(null);

            Assert.True(service.TryResolveLangText("", "zh-TW", PolhemUIText.Namespace, PolhemUIText.Save, out string text));
            Assert.Equal("儲存", text);
        }

        [Fact]
        [DisplayName("A culture with no shipped translation misses, so the caller keeps its English base text")]
        public void TryResolveLangText_NoTranslation_Misses()
        {
            var service = new FrameworkLanguageService(null);

            Assert.False(service.TryResolveLangText("", "fr-FR", PolhemUIText.Namespace, PolhemUIText.Save, out _));
        }

        [Fact]
        [DisplayName("The default language supplied by the provider is the last hop of the chain")]
        public void TryResolveLangText_DefaultLanguageProvider_IsLastHop()
        {
            var service = new FrameworkLanguageService(null, () => "zh-TW");

            Assert.Equal("zh-TW", service.DefaultLanguage);
            Assert.True(service.TryResolveLangText("", "fr-FR", PolhemUIText.Namespace, PolhemUIText.Cancel, out string text));
            Assert.Equal("取消", text);
        }

        [Fact]
        [DisplayName("An English culture ends the chain for the framework's own text, so English users never get the default language")]
        public void TryResolveLangText_EnglishCulture_StopsBeforeDefaultLanguage()
        {
            var service = new FrameworkLanguageService(null, () => "zh-TW");

            Assert.False(service.TryResolveLangText("", "en-GB", PolhemUIText.Namespace, PolhemUIText.Save, out _));
            Assert.False(service.TryResolveLangText("", "en-US", PolhemMessages.Namespace, "Login.InvalidCredentials", out _));
        }

        [Fact]
        [DisplayName("A definition namespace follows the same rule: en-GB stops at English, fr-FR reaches the zh-TW default")]
        public void TryResolveLangText_DefinitionNamespace_SameEnglishStop()
        {
            var host = new DictionaryLanguageService("zh-TW");
            host.Add("zh-TW", "Order", "Schema.DisplayName", "訂單");
            var service = new FrameworkLanguageService(host);

            Assert.False(service.TryResolveLangText("", "en-GB", "Order", "Schema.DisplayName", out _));
            Assert.True(service.TryResolveLangText("", "fr-FR", "Order", "Schema.DisplayName", out string text));
            Assert.Equal("訂單", text);
        }

        [Fact]
        [DisplayName("A host resource wins over the shipped translation for the same key and culture")]
        public void TryGetLangText_HostKey_WinsOverShipped()
        {
            var host = new DictionaryLanguageService("zh-TW");
            host.Add("zh-TW", PolhemUIText.Namespace, PolhemUIText.Save, "存檔");
            var service = new FrameworkLanguageService(host);

            Assert.Equal("存檔", service.GetLangText("zh-TW", PolhemUIText.Namespace, PolhemUIText.Save));
            // A key the host does not declare still comes from the shipped translation.
            Assert.Equal("取消", service.GetLangText("zh-TW", PolhemUIText.Namespace, PolhemUIText.Cancel));
        }

        [Fact]
        [DisplayName("A host translation in the user's parent culture wins over a shipped one in the default language")]
        public void TryResolveLangText_HostParentCulture_BeatsShippedDefault()
        {
            var host = new DictionaryLanguageService("zh-TW");
            host.Add("fr", PolhemUIText.Namespace, PolhemUIText.Save, "Enregistrer");
            var service = new FrameworkLanguageService(host);

            Assert.True(service.TryResolveLangText("", "fr-CA", PolhemUIText.Namespace, PolhemUIText.Save, out string text));
            Assert.Equal("Enregistrer", text);
        }

        [Fact]
        [DisplayName("GetBuiltIn matches the culture of the shipped resource ignoring case")]
        public void GetBuiltIn_CultureCasing_IsIgnored()
        {
            Assert.NotNull(FrameworkLanguageService.GetBuiltIn("ZH-tw", PolhemMessages.Namespace));
            Assert.Null(FrameworkLanguageService.GetBuiltIn("zh-TW", "NoSuchNamespace"));
        }

        /// <summary>
        /// A minimal host service over an in-memory table, so the tests control exactly which keys
        /// the host declares.
        /// </summary>
        private sealed class DictionaryLanguageService(string defaultLanguage) : ILanguageService
        {
            private readonly Dictionary<string, string> _texts = new(StringComparer.OrdinalIgnoreCase);

            public string DefaultLanguage { get; } = defaultLanguage;

            public void Add(string lang, string ns, string subKey, string text) => _texts[$"{lang}|{ns}|{subKey}"] = text;

            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            {
                bool hit = _texts.TryGetValue($"{lang}|{@namespace}|{subKey}", out string? value);
                text = value ?? string.Empty;
                return hit;
            }

            public bool TryGetLangText(string lang, string fullKey, out string text)
            {
                (string ns, string subKey) = LanguageKey.Split(fullKey);
                return TryGetLangText(lang, ns, subKey, out text);
            }

            public string GetLangText(string lang, string fullKey) => throw new NotSupportedException();
            public string GetLangText(string lang, string @namespace, string subKey) => throw new NotSupportedException();
            public LanguageEnum? GetLangEnum(string lang, string fullName) => null;
            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => null;
            public string? GetLangEnumText(string lang, string fullName, string code) => null;
        }
    }
}
