using System.ComponentModel;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Tests for <see cref="MenuLocalizer"/>: menu captions from the <c>Menu</c> namespace, keyed by
    /// node id, with the node's own caption as the base text.
    /// </summary>
    public class MenuLocalizerTests
    {
        [Fact]
        [DisplayName("A folder and an entry take their captions from Folder.{Id}.Caption and Entry.{Id}.Caption")]
        public void GetCaption_KeysByNodeKind()
        {
            var service = new TableLanguageService("");
            service.Add("zh-TW", "Folder.sales.Caption", "銷售");
            service.Add("zh-TW", "Entry.orders.Caption", "訂單");
            var localizer = new MenuLocalizer(service);

            Assert.Equal("銷售", localizer.GetCaption(new MenuFolder("sales", "Sales"), "zh-TW"));
            Assert.Equal("訂單", localizer.GetCaption(new MenuEntry("orders", "Order", "Orders"), "zh-TW"));
        }

        [Fact]
        [DisplayName("A node with no translation in any culture of the chain keeps its own caption")]
        public void GetCaption_NoTranslation_KeepsBaseCaption()
        {
            var service = new TableLanguageService("zh-TW");
            service.Add("zh-TW", "Entry.other.Caption", "其他");
            var localizer = new MenuLocalizer(service);

            Assert.Equal("Orders", localizer.GetCaption(new MenuEntry("orders", "Order", "Orders"), "fr-FR"));
        }

        [Fact]
        [DisplayName("An entry caption falls back to the default language for fr-FR, and to the base caption for en-GB")]
        public void GetCaption_MissingInCulture_UsesDefaultLanguageExceptForEnglish()
        {
            var service = new TableLanguageService("zh-TW");
            service.Add("zh-TW", "Entry.orders.Caption", "訂單");
            var localizer = new MenuLocalizer(service);
            var entry = new MenuEntry("orders", "Order", "Orders");

            Assert.Equal("訂單", localizer.GetCaption(entry, "fr-FR"));
            Assert.Equal("Orders", localizer.GetCaption(entry, "en-GB"));
        }

        [Fact]
        [DisplayName("The same program under two node ids takes two different captions")]
        public void GetCaption_SameProgramTwice_KeyedById()
        {
            var service = new TableLanguageService("");
            service.Add("en-US", "Entry.orders.Caption", "Orders");
            service.Add("en-US", "Entry.orders-draft.Caption", "Draft orders");
            var localizer = new MenuLocalizer(service);

            Assert.Equal("Draft orders", localizer.GetCaption(new MenuEntry("orders-draft", "Order", "x"), "en-US"));
            Assert.Equal("Orders", localizer.GetCaption(new MenuEntry("orders", "Order", "x"), "en-US"));
        }

        /// <summary>
        /// An in-memory <c>Menu</c> namespace; the chain comes from the interface's default
        /// <see cref="ILanguageService.TryResolveLangText"/>.
        /// </summary>
        private sealed class TableLanguageService(string defaultLanguage) : ILanguageService
        {
            private readonly Dictionary<string, string> _texts = new(StringComparer.OrdinalIgnoreCase);

            public string DefaultLanguage { get; } = defaultLanguage;

            public void Add(string lang, string subKey, string text) => _texts[$"{lang}|{subKey}"] = text;

            public bool TryGetLangText(string lang, string @namespace, string subKey, out string text)
            {
                bool hit = @namespace == MenuLocalizer.Namespace && _texts.TryGetValue($"{lang}|{subKey}", out string? value);
                text = hit ? _texts[$"{lang}|{subKey}"] : string.Empty;
                return hit;
            }

            public bool TryGetLangText(string lang, string fullKey, out string text) => throw new NotSupportedException();
            public string GetLangText(string lang, string fullKey) => throw new NotSupportedException();
            public string GetLangText(string lang, string @namespace, string subKey) => throw new NotSupportedException();
            public LanguageEnum? GetLangEnum(string lang, string fullName) => null;
            public LanguageEnum? GetLangEnum(string lang, string @namespace, string enumName) => null;
            public string? GetLangEnumText(string lang, string fullName, string code) => null;
        }
    }
}
