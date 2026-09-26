using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the default value of <see cref="BackendConfiguration.DefaultLanguage"/>.
    /// </summary>
    /// <remarks>
    /// As with <see cref="BackendConfigurationTimeZoneTests"/>, the default is a deliberate compatibility choice:
    /// SessionInfo.Culture used to be hard-coded to zh-TW, so existing deployments all run in that culture. Now that a user
    /// attribute decides it, users without a value must fall back to the same value, or the whole language changes after the
    /// upgrade.
    /// </remarks>
    public class BackendConfigurationDefaultLanguageTests
    {
        [Fact]
        [DisplayName("DefaultLanguage defaults to zh-TW (upgrade compatibility)")]
        public void DefaultLanguage_Default_IsZhTw()
        {
            var config = new BackendConfiguration();

            Assert.Equal("zh-TW", config.DefaultLanguage);
        }

        [Fact]
        [DisplayName("DefaultLanguage can be set to an empty string, leaving the default to the language service")]
        public void DefaultLanguage_CanBeCleared()
        {
            var config = new BackendConfiguration { DefaultLanguage = string.Empty };

            Assert.Empty(config.DefaultLanguage);
        }
    }
}
