using System.ComponentModel;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Verifies that the default interface implementations of <see cref="ILanguageService"/> (GetLangText, TryGetLangText, GetLangEnum, GetLangEnumText) delegate correctly.
    /// Uses a minimal implementation that does not override these default methods, and calls through the interface type to hit the default implementation paths.
    /// </summary>
    public class ILanguageServiceDefaultImplTests
    {
        private sealed class MinimalLanguageService : ILanguageService
        {
            public string GetLangText(string lang, string fullKey)
                => $"{lang}:{fullKey}";

            public string GetLangText(string lang, string @namespace, string subKey)
                => $"{lang}:{@namespace}.{subKey}";

            public bool TryGetLangText(string lang, string fullKey, out string text)
            {
                text = $"found:{fullKey}";
                return true;
            }

            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            {
                text = $"found:{@namespace}.{subKey}";
                return true;
            }

            public LanguageEnum? GetLangEnum(string lang, string fullName)
                => new() { Name = fullName };

            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName)
                => new() { Name = enumName };

            public string? GetLangEnumText(string lang, string fullName, string code)
                => $"{fullName}:{code}";
        }

        [Fact]
        [DisplayName("ILanguageService default GetLangText (4 parameters) delegates to GetLangText (3 parameters)")]
        public void GetLangText_DefaultImpl4Args_DelegatesTo3ArgOverload()
        {
            ILanguageService svc = new MinimalLanguageService();
            var result = svc.GetLangText("cust", "zh-TW", "Common", "OK");
            Assert.Equal("zh-TW:Common.OK", result);
        }

        [Fact]
        [DisplayName("ILanguageService default TryGetLangText (5 parameters) delegates to TryGetLangText (4 parameters)")]
        public void TryGetLangText_DefaultImpl5Args_DelegatesTo4ArgOverload()
        {
            ILanguageService svc = new MinimalLanguageService();
            bool hit = svc.TryGetLangText("cust", "zh-TW", "Common", "OK", out string text);
            Assert.True(hit);
            Assert.Equal("found:Common.OK", text);
        }

        [Fact]
        [DisplayName("ILanguageService default GetLangEnum (4 parameters) delegates to GetLangEnum (3 parameters)")]
        public void GetLangEnum_DefaultImpl4Args_DelegatesTo3ArgOverload()
        {
            ILanguageService svc = new MinimalLanguageService();
            var result = svc.GetLangEnum("cust", "zh-TW", "Common", "Gender");
            Assert.NotNull(result);
            Assert.Equal("Gender", result!.Name);
        }

        [Fact]
        [DisplayName("ILanguageService default GetLangEnumText (4 parameters) delegates to GetLangEnumText (3 parameters)")]
        public void GetLangEnumText_DefaultImpl4Args_DelegatesTo3ArgOverload()
        {
            ILanguageService svc = new MinimalLanguageService();
            var result = svc.GetLangEnumText("cust", "zh-TW", "Common.Gender", "M");
            Assert.Equal("Common.Gender:M", result);
        }
    }
}
