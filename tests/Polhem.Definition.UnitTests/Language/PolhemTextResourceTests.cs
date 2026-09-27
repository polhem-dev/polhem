using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Polhem.Definition.Language;

namespace Polhem.Definition.UnitTests.Language
{
    /// <summary>
    /// Keeps the keys of <see cref="PolhemUIText"/> and <see cref="PolhemMessages"/>, the English base
    /// text, and the translations shipped inside the framework in step. The XML docs of both types name this class as the check.
    /// </summary>
    public class PolhemTextResourceTests
    {
        private static readonly string[] s_shippedCultures = ["zh-TW"];

        [Fact]
        [DisplayName("UI text: every UI text key has English base text and nothing else does")]
        public void PolhemUIText_KeysAndDefaultTexts_Match()
        {
            var keys = KeyConstants(typeof(PolhemUIText));

            Assert.Equal(keys.Order(StringComparer.Ordinal), PolhemUIText.DefaultTexts.Keys.Order(StringComparer.Ordinal));
        }

        [Theory]
        [MemberData(nameof(ShippedCultures))]
        [DisplayName("UI text: the shipped translation covers every UI text key and names no other")]
        public void PolhemUIText_ShippedTranslation_CoversEveryKey(string culture)
        {
            var resource = FrameworkLanguageService.GetBuiltIn(culture, PolhemUIText.Namespace);
            Assert.NotNull(resource);

            var translated = resource!.Items.Select(i => i.Key).Order(StringComparer.Ordinal);
            Assert.Equal(KeyConstants(typeof(PolhemUIText)).Order(StringComparer.Ordinal), translated);
            Assert.Equal(culture, resource.Lang);
            Assert.Equal(PolhemUIText.Namespace, resource.Namespace);
        }

        [Fact]
        [DisplayName("Messages: every message key lives in the PolhemMessages namespace")]
        public void PolhemMessages_Keys_AreInTheirNamespace()
        {
            foreach (string key in KeyConstants(typeof(PolhemMessages)))
            {
                Assert.Equal(PolhemMessages.Namespace, LanguageKey.Split(key).Namespace);
            }
        }

        [Theory]
        [MemberData(nameof(ShippedCultures))]
        [DisplayName("Messages: the shipped translation covers every message key, names no other, and formats cleanly")]
        public void PolhemMessages_ShippedTranslation_CoversEveryKey(string culture)
        {
            var resource = FrameworkLanguageService.GetBuiltIn(culture, PolhemMessages.Namespace);
            Assert.NotNull(resource);

            var expected = KeyConstants(typeof(PolhemMessages))
                .Select(key => LanguageKey.Split(key).SubKey)
                .Order(StringComparer.Ordinal);
            Assert.Equal(expected, resource!.Items.Select(i => i.Key).Order(StringComparer.Ordinal));

            // A placeholder the throw sites cannot fill would fall back to English at run time; the
            // arguments any message takes are at most three.
            object[] arguments = ["a", "b", "c"];
            foreach (var item in resource.Items)
            {
                string formatted = string.Format(CultureInfo.InvariantCulture, item.Value, arguments);
                Assert.False(string.IsNullOrWhiteSpace(formatted), item.Key);
            }
        }

        public static TheoryData<string> ShippedCultures()
        {
            var data = new TheoryData<string>();
            foreach (string culture in s_shippedCultures) { data.Add(culture); }
            return data;
        }

        private static List<string> KeyConstants(Type type)
            => type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != "Namespace")
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();
    }
}
