using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Definition.Settings;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Tests for how <see cref="SystemApiConnector"/> adopts the server's configuration, which arrives over an
    /// anonymous Plain call that nothing authenticates.
    /// </summary>
    /// <remarks>
    /// Driven through <c>AdoptServerConfiguration</c> rather than <c>InitializeAsync</c>, so no test touches the
    /// process-wide <c>SysInfo</c> and <c>ApiServiceOptions</c> statics.
    /// </remarks>
    public class SystemApiConnectorConfigurationTests
    {
        private static readonly string[] s_clientNamespaces = ["Polhem.Base", "Polhem.Definition"];

        private static CommonConfiguration ServerConfiguration(string encryptor, bool isDebugMode = true,
            string allowedTypeNamespaces = "")
            => new()
            {
                Version = "9.9.9",
                IsDebugMode = isDebugMode,
                AllowedTypeNamespaces = allowedTypeNamespaces,
                ApiPayloadOptions = new ApiPayloadOptions { Compressor = "gzip", Encryptor = encryptor },
            };

        [Theory]
        [InlineData("none")]
        [InlineData("")]
        [DisplayName("A server advertising no encryption is refused when the client is not in debug mode")]
        public void AdoptServerConfiguration_NoEncryption_ClientNotDebug_Throws(string encryptor)
        {
            Assert.Throws<InvalidOperationException>(() =>
                SystemApiConnector.AdoptServerConfiguration(ServerConfiguration(encryptor), clientIsDebugMode: false, s_clientNamespaces));
        }

        [Fact]
        [DisplayName("A server advertising no encryption is accepted when the client itself is in debug mode")]
        public void AdoptServerConfiguration_NoEncryption_ClientDebug_IsAccepted()
        {
            var adopted = SystemApiConnector.AdoptServerConfiguration(
                ServerConfiguration("none"), clientIsDebugMode: true, s_clientNamespaces);

            Assert.Equal("none", adopted.ApiPayloadOptions.Encryptor);
            Assert.True(adopted.IsDebugMode);
        }

        [Fact]
        [DisplayName("The server's debug flag does not switch the client into debug mode")]
        public void AdoptServerConfiguration_ServerDebug_DoesNotRaiseClientDebug()
        {
            var adopted = SystemApiConnector.AdoptServerConfiguration(
                ServerConfiguration("aes-cbc-hmac", isDebugMode: true), clientIsDebugMode: false, s_clientNamespaces);

            Assert.False(adopted.IsDebugMode);
            Assert.Equal("aes-cbc-hmac", adopted.ApiPayloadOptions.Encryptor);
            Assert.Equal("9.9.9", adopted.Version);
        }

        [Fact]
        [DisplayName("The server cannot widen the client's allowed type namespaces")]
        public void AdoptServerConfiguration_ServerNamespaces_AreIgnored()
        {
            var adopted = SystemApiConnector.AdoptServerConfiguration(
                ServerConfiguration("aes-cbc-hmac", allowedTypeNamespaces: "System.Diagnostics|Evil.Gadgets"),
                clientIsDebugMode: false, s_clientNamespaces);

            Assert.Equal("Polhem.Base|Polhem.Definition", adopted.AllowedTypeNamespaces);
            Assert.DoesNotContain("Evil", adopted.AllowedTypeNamespaces, StringComparison.Ordinal);
        }
    }
}
