using System.ComponentModel;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.Transformers;
using Polhem.Definition.Settings;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Core.UnitTests.Payload
{
    /// <summary>
    /// How <see cref="PolhemPayload"/> turns the framework's payload settings into the options of the payload package.
    /// </summary>
    public class PolhemPayloadTests
    {
        [Fact]
        [DisplayName("The framework's options default to gzip, aes-cbc-hmac, messagepack and the framework's type names, with no frame")]
        public void CreateOptions_Defaults_MatchTheFramework()
        {
            var options = PolhemPayload.CreateOptions();

            Assert.IsType<GzipPayloadCompressor>(options.Compressor);
            Assert.IsType<AesCbcHmacPayloadEncryptor>(options.Encryptor);
            Assert.False(options.AllowNoEncryption);
            Assert.Equal(PayloadCodecNames.MessagePack, options.DefaultCodec);
            Assert.Same(PolhemPayloadTypeResolver.Instance, options.TypeResolver);
            Assert.False(options.RequireFrame);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("")]
        [DisplayName("The compressor names none and blank select no compression")]
        public void Apply_NoCompressor_SelectsNoCompression(string name)
        {
            var options = PolhemPayload.CreateOptions(new ApiPayloadOptions { Compressor = name });

            Assert.Same(NoPayloadCompressor.Instance, options.Compressor);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("")]
        [DisplayName("The encryptor none is allowed in debug mode, and switches the package's guard off only then")]
        public void Apply_NoEncryptorInDebugMode_IsAllowed(string name)
        {
            var options = PolhemPayload.CreateOptions(new ApiPayloadOptions { Encryptor = name }, isDebugMode: true);

            Assert.Same(NoPayloadEncryptor.Instance, options.Encryptor);
            Assert.True(options.AllowNoEncryption);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("")]
        [DisplayName("The encryptor none is refused outside debug mode")]
        public void Apply_NoEncryptorOutsideDebugMode_Throws(string name)
        {
            Assert.Throws<InvalidOperationException>(
                () => PolhemPayload.CreateOptions(new ApiPayloadOptions { Encryptor = name }, isDebugMode: false));
        }

        [Fact]
        [DisplayName("Applying aes-cbc-hmac after none switches the package's guard back on")]
        public void Apply_AesAfterNone_ClearsAllowNoEncryption()
        {
            var options = PolhemPayload.CreateOptions(new ApiPayloadOptions { Encryptor = "none" }, isDebugMode: true);

            PolhemPayload.Apply(options, new ApiPayloadOptions { Encryptor = "aes-cbc-hmac" }, isDebugMode: true);

            Assert.IsType<AesCbcHmacPayloadEncryptor>(options.Encryptor);
            Assert.False(options.AllowNoEncryption);
        }

        [Fact]
        [DisplayName("Apply changes the compressor and encryptor and leaves the frame setting as it was")]
        public void Apply_KeepsRequireFrame()
        {
            var options = PolhemPayload.CreateOptions();
            options.RequireFrame = true;

            PolhemPayload.Apply(options, new ApiPayloadOptions { Compressor = "none" }, isDebugMode: false);

            Assert.True(options.RequireFrame);
            Assert.Same(NoPayloadCompressor.Instance, options.Compressor);
        }

        [Fact]
        [DisplayName("Unknown compressor and encryptor names are not supported")]
        public void Apply_UnknownNames_Throw()
        {
            Assert.Throws<NotSupportedException>(() => PolhemPayload.CreateOptions(new ApiPayloadOptions { Compressor = "brotli" }));
            Assert.Throws<NotSupportedException>(() => PolhemPayload.CreateOptions(new ApiPayloadOptions { Encryptor = "rot13" }));
        }

        [Fact]
        [DisplayName("messagepack is registered by the framework, so a host cannot register another codec under its name")]
        public void RegisterCodec_MessagePackName_Throws()
        {
            var options = PolhemPayload.CreateOptions();

            Assert.Throws<InvalidOperationException>(() => options.RegisterCodec(new MessagePackPayloadCodec()));
        }
    }
}
