using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for <see cref="CommonConfiguration.DefaultLanguage"/>, the one default-language setting:
    /// the culture of users without one and the last hop of the language fall-back chain.
    /// </summary>
    public class CommonConfigurationDefaultLanguageTests
    {
        [Fact]
        [DisplayName("DefaultLanguage defaults to zh-TW")]
        public void DefaultLanguage_Default_IsZhTw()
        {
            var config = new CommonConfiguration();

            Assert.Equal("zh-TW", config.DefaultLanguage);
        }

        [Fact]
        [DisplayName("DefaultLanguage can be set to an empty string, which drops the default-language hop")]
        public void DefaultLanguage_CanBeCleared()
        {
            var config = new CommonConfiguration { DefaultLanguage = string.Empty };

            Assert.Empty(config.DefaultLanguage);
        }

        [Fact]
        [DisplayName("The shipped SystemSettings template states DefaultLanguage explicitly and it reads back as zh-TW")]
        public void DefaultsTemplate_DeclaresDefaultLanguage()
        {
            using var stream = Polhem.Definition.Defaults.OpenEmbedded("SystemSettings.xml");
            using var reader = new StreamReader(stream);
            string xml = reader.ReadToEnd();

            var settings = XmlCodec.Deserialize<SystemSettings>(xml)!;

            Assert.Contains("<DefaultLanguage>zh-TW</DefaultLanguage>", xml, StringComparison.Ordinal);
            Assert.Equal("zh-TW", settings.CommonConfiguration.DefaultLanguage);
            Assert.Equal("Asia/Taipei", settings.BackendConfiguration.DefaultTimeZone);
        }
    }
}
