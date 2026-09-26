using System.ComponentModel;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Unit tests for ApiPayloadOptions.
    /// </summary>
    public class ApiPayloadOptionsTests
    {
        [Fact]
        [DisplayName("The default constructor initializes the default Compressor and Encryptor")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var options = new ApiPayloadOptions();

            Assert.Equal("gzip", options.Compressor);
            Assert.Equal("aes-cbc-hmac", options.Encryptor);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var options = new ApiPayloadOptions
            {
                Compressor = "none",
                Encryptor = "none"
            };

            Assert.Equal("none", options.Compressor);
            Assert.Equal("none", options.Encryptor);
        }

        [Fact]
        [DisplayName("ToString returns the full settings string")]
        public void ToString_ReturnsFormatted()
        {
            var options = new ApiPayloadOptions
            {
                Compressor = "gzip",
                Encryptor = "aes-cbc-hmac"
            };

            Assert.Equal(
                "Compressor: gzip, Encryptor: aes-cbc-hmac",
                options.ToString());
        }
    }
}
