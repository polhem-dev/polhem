using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the simple configuration classes (WebsiteConfiguration, FrontendConfiguration and others).
    /// </summary>
    public class SystemConfigurationTests
    {
        [Fact]
        [DisplayName("BackgroundServiceConfiguration.ToString returns the type name")]
        public void BackgroundServiceConfiguration_ToString_ReturnsTypeName()
        {
            var config = new BackgroundServiceConfiguration();

            Assert.Equal(nameof(BackgroundServiceConfiguration), config.ToString());
        }

        [Fact]
        [DisplayName("FrontendConfiguration.ToString returns the type name")]
        public void FrontendConfiguration_ToString_ReturnsTypeName()
        {
            var config = new FrontendConfiguration();

            Assert.Equal(nameof(FrontendConfiguration), config.ToString());
        }

        [Fact]
        [DisplayName("WebsiteConfiguration.ToString returns the type name")]
        public void WebsiteConfiguration_ToString_ReturnsTypeName()
        {
            var config = new WebsiteConfiguration();

            Assert.Equal(nameof(WebsiteConfiguration), config.ToString());
        }

        [Fact]
        [DisplayName("BackendConfiguration.ToString returns the type name")]
        public void BackendConfiguration_ToString_ReturnsTypeName()
        {
            var config = new BackendConfiguration();

            Assert.Equal(nameof(BackendConfiguration), config.ToString());
        }
    }
}
