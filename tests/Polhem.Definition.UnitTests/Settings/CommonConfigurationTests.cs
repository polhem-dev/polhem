using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Unit tests for CommonConfiguration.
    /// </summary>
    public class CommonConfigurationTests
    {
        [Fact]
        [DisplayName("The default constructor initializes the default values")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var config = new CommonConfiguration();

            Assert.Equal(string.Empty, config.Version);
            Assert.False(config.IsDebugMode);
            Assert.Equal(string.Empty, config.AllowedTypeNamespaces);
            Assert.NotNull(config.ApiPayloadOptions);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var payload = new ApiPayloadOptions { Compressor = "none" };
            var config = new CommonConfiguration
            {
                Version = "4.0.1",
                IsDebugMode = true,
                AllowedTypeNamespaces = "Custom.Module|ThirdParty.Dto",
                ApiPayloadOptions = payload
            };

            Assert.Equal("4.0.1", config.Version);
            Assert.True(config.IsDebugMode);
            Assert.Equal("Custom.Module|ThirdParty.Dto", config.AllowedTypeNamespaces);
            Assert.Same(payload, config.ApiPayloadOptions);
        }

        [Fact]
        [DisplayName("ToString returns the type name")]
        public void ToString_ReturnsTypeName()
        {
            var config = new CommonConfiguration();

            Assert.Equal(nameof(CommonConfiguration), config.ToString());
        }
    }
}
