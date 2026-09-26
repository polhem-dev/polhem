using System.ComponentModel;
using Polhem.Api.Core.Transformers;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// ApiPayloadOptionsFactory tests.
    /// </summary>
    public class ApiPayloadOptionsFactoryTests
    {

        [Fact]
        [DisplayName("CreateCompressor(\"gzip\") returns GzipPayloadCompressor")]
        public void CreateCompressor_Gzip_ReturnsGzipCompressor()
        {
            var compressor = ApiPayloadOptionsFactory.CreateCompressor("gzip");

            Assert.IsType<GzipPayloadCompressor>(compressor);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("")]
        [DisplayName("CreateCompressor(\"none\"/\"\") returns NoCompressionCompressor")]
        public void CreateCompressor_None_ReturnsNoCompressionCompressor(string name)
        {
            var compressor = ApiPayloadOptionsFactory.CreateCompressor(name);

            Assert.IsType<NoCompressionCompressor>(compressor);
        }

        [Fact]
        [DisplayName("CreateCompressor throws NotSupportedException for an unsupported name")]
        public void CreateCompressor_Unknown_Throws()
        {
            Assert.Throws<NotSupportedException>(() => ApiPayloadOptionsFactory.CreateCompressor("lzma"));
        }

        [Fact]
        [DisplayName("CreateEncryptor(\"aes-cbc-hmac\") returns AesPayloadEncryptor")]
        public void CreateEncryptor_AesCbcHmac_ReturnsAesPayloadEncryptor()
        {
            var encryptor = ApiPayloadOptionsFactory.CreateEncryptor("aes-cbc-hmac", isDebugMode: false);

            Assert.IsType<AesPayloadEncryptor>(encryptor);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("")]
        [DisplayName("CreateEncryptor(\"none\"/\"\") returns NoEncryptionEncryptor in debug mode")]
        public void CreateEncryptor_None_DebugMode_ReturnsNoEncryptionEncryptor(string name)
        {
            var encryptor = ApiPayloadOptionsFactory.CreateEncryptor(name, isDebugMode: true);

            Assert.IsType<NoEncryptionEncryptor>(encryptor);
        }

        [Fact]
        [DisplayName("CreateEncryptor(\"none\") throws InvalidOperationException outside debug mode")]
        public void CreateEncryptor_None_ProductionMode_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => ApiPayloadOptionsFactory.CreateEncryptor("none", isDebugMode: false));
        }

        [Fact]
        [DisplayName("CreateEncryptor throws NotSupportedException for an unsupported name")]
        public void CreateEncryptor_Unknown_Throws()
        {
            Assert.Throws<NotSupportedException>(() => ApiPayloadOptionsFactory.CreateEncryptor("rsa", isDebugMode: true));
        }
    }
}
