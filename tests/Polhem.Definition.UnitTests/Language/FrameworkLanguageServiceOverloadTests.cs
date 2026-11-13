using System.ComponentModel;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Tests for the key-splitting overloads and the option-set lookups of <see cref="FrameworkLanguageService"/>.
    /// </summary>
    public class FrameworkLanguageServiceOverloadTests
    {
        private static readonly string s_saveKey = $"{PolhemUIText.Namespace}.{PolhemUIText.Save}";

        [Fact]
        [DisplayName("The constructor rejects a null default language provider")]
        public void Constructor_NullDefaultLanguageProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FrameworkLanguageService(null, null!));
        }

        [Fact]
        [DisplayName("Without an inner service or a provider the default language is empty")]
        public void DefaultLanguage_NoInnerNoProvider_IsEmpty()
        {
            Assert.Equal(string.Empty, new FrameworkLanguageService(null).DefaultLanguage);
        }

        [Fact]
        [DisplayName("Without a provider the default language is the inner service's")]
        public void DefaultLanguage_InnerOnly_ComesFromInner()
        {
            Assert.Equal("zh-TW", new FrameworkLanguageService(new StubLanguageService("zh-TW")).DefaultLanguage);
        }

        [Theory]
        [InlineData("", "Polhem.UI")]
        [InlineData("zh-TW", " ")]
        [DisplayName("GetBuiltIn returns null for a blank culture or namespace")]
        public void GetBuiltIn_BlankArgument_ReturnsNull(string lang, string ns)
        {
            Assert.Null(FrameworkLanguageService.GetBuiltIn(lang, ns));
        }

        [Fact]
        [DisplayName("GetLangText with a full key splits it and resolves the shipped translation")]
        public void GetLangText_FullKey_ResolvesShippedTranslation()
        {
            var service = new FrameworkLanguageService(null);

            Assert.Equal("儲存", service.GetLangText("zh-TW", s_saveKey));
        }

        [Fact]
        [DisplayName("GetLangText returns the namespace-qualified key when no culture of the chain has the text")]
        public void GetLangText_Miss_ReturnsQualifiedKey()
        {
            var service = new FrameworkLanguageService(null);

            Assert.Equal("NoSuchNamespace.Missing", service.GetLangText("zh-TW", "NoSuchNamespace", "Missing"));
        }

        [Fact]
        [DisplayName("TryGetLangText with a full key looks up the single culture given")]
        public void TryGetLangText_FullKey_LooksUpSingleCulture()
        {
            var service = new FrameworkLanguageService(null);

            Assert.True(service.TryGetLangText("zh-TW", s_saveKey, out string text));
            Assert.Equal("儲存", text);
        }

        [Fact]
        [DisplayName("TryGetLangText with a namespace and sub-key reports a miss with an empty text")]
        public void TryGetLangText_NamespaceAndSubKey_MissReturnsEmpty()
        {
            var service = new FrameworkLanguageService(null);

            Assert.False(service.TryGetLangText("fr-FR", PolhemUIText.Namespace, PolhemUIText.Save, out string text));
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        [DisplayName("GetLangEnum returns the inner service's option set first")]
        public void GetLangEnum_InnerHit_ReturnsInnerEnum()
        {
            var inner = new StubLanguageService("zh-TW") { Enum = CreateEnum("Status") };
            var service = new FrameworkLanguageService(inner);

            Assert.Same(inner.Enum, service.GetLangEnum("zh-TW", "Order.Status"));
            Assert.Same(inner.Enum, service.GetLangEnum("zh-TW", "Order", "Status"));
        }

        [Fact]
        [DisplayName("GetLangEnum walks the chain through the shipped resources and returns null when nothing declares the set")]
        public void GetLangEnum_NoInnerAndNoShippedSet_ReturnsNull()
        {
            var service = new FrameworkLanguageService(null, () => "zh-TW");

            Assert.Null(service.GetLangEnum("fr-FR", PolhemUIText.Namespace, "Status"));
        }

        [Fact]
        [DisplayName("GetLangEnum falls through to the shipped resources when the inner service has no option set")]
        public void GetLangEnum_InnerMiss_ReturnsNull()
        {
            var service = new FrameworkLanguageService(new StubLanguageService("zh-TW"));

            Assert.Null(service.GetLangEnum("", "zh-TW", "NoSuchNamespace", "Status"));
        }

        [Fact]
        [DisplayName("GetLangEnumText returns the text of a code from the resolved option set")]
        public void GetLangEnumText_KnownCode_ReturnsText()
        {
            var inner = new StubLanguageService("zh-TW") { Enum = CreateEnum("Status") };
            var service = new FrameworkLanguageService(inner);

            Assert.Equal("Active", service.GetLangEnumText("zh-TW", "Order.Status", "A"));
        }

        [Fact]
        [DisplayName("GetLangEnumText returns null when no option set resolves")]
        public void GetLangEnumText_NoEnum_ReturnsNull()
        {
            var service = new FrameworkLanguageService(null);

            Assert.Null(service.GetLangEnumText("zh-TW", "Order.Status", "A"));
        }

        private static LanguageEnum CreateEnum(string name)
        {
            var languageEnum = new LanguageEnum { Name = name };
            languageEnum.Entries!.Add(new LanguageEnumEntry { Code = "A", Text = "Active" });
            return languageEnum;
        }

        /// <summary>
        /// A host service that declares no text and, optionally, one option set for every lookup.
        /// </summary>
        private sealed class StubLanguageService(string defaultLanguage) : ILanguageService
        {
            public LanguageEnum? Enum { get; init; }

            public string DefaultLanguage { get; } = defaultLanguage;

            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            {
                text = string.Empty;
                return false;
            }

            public bool TryGetLangText(string lang, string fullKey, out string text)
            {
                text = string.Empty;
                return false;
            }

            public string GetLangText(string lang, string fullKey) => fullKey;
            public string GetLangText(string lang, string @namespace, string subKey) => $"{@namespace}.{subKey}";
            public LanguageEnum? GetLangEnum(string lang, string fullName) => Enum;
            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => Enum;
            public string? GetLangEnumText(string lang, string fullName, string code) => Enum?.GetText(code);
        }
    }
}
