using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Base;
using Polhem.Base.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the file-exists path of <see cref="ClientInfo.LoadClientSettings"/>,
    /// and that the <c>AccessToken</c> setter does not reset the connector cache when given the same token.
    /// It mutates static state, so it shares the collection with the other ClientInfoState tests to run serially.
    /// </summary>
    [Collection("ClientInfoState")]
    public class ClientInfoLoadSettingsTests
    {
        private static readonly FieldInfo s_clientSettingsField =
            typeof(ClientInfo).GetField("s_clientSettings", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static readonly FieldInfo s_systemConnectorField =
            typeof(ClientInfo).GetField("s_systemConnector", BindingFlags.NonPublic | BindingFlags.Static)!;

        [Fact]
        [DisplayName("LoadClientSettings deserializes an existing settings file and returns a non-null ClientSettings")]
        public void ClientSettings_FileExists_ReturnsDeserializedSettings()
        {
            string exeName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Client";
            string fileName = $"{exeName}.Settings.xml";
            string filePath = Path.Combine(FileUtilities.GetAssemblyPath(), fileName);
            var originalCached = s_clientSettingsField.GetValue(null);

            XmlCodec.SerializeToFile(new ClientSettings(), filePath);
            try
            {
                s_clientSettingsField.SetValue(null, null);
                var result = ClientInfo.ClientSettings;
                Assert.NotNull(result);
            }
            finally
            {
                s_clientSettingsField.SetValue(null, originalCached);
                try { File.Delete(filePath); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("LoadClientSettings throws InvalidOperationException for an empty settings file (deserialization returns null)")]
        public void ClientSettings_EmptyFile_ThrowsInvalidOperationException()
        {
            string exeName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Client";
            string fileName = $"{exeName}.Settings.xml";
            string filePath = Path.Combine(FileUtilities.GetAssemblyPath(), fileName);
            var originalCached = s_clientSettingsField.GetValue(null);

            File.WriteAllText(filePath, string.Empty);
            try
            {
                s_clientSettingsField.SetValue(null, null);
                var exception = Record.Exception(() => _ = ClientInfo.ClientSettings);
                Assert.NotNull(exception);
                Assert.IsType<InvalidOperationException>(exception);
            }
            finally
            {
                s_clientSettingsField.SetValue(null, originalCached);
                try { File.Delete(filePath); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("AccessToken setter does not reset the _systemConnector cache when given the same token")]
        public void AccessToken_SameTokenSetTwice_ConnectorCachePreserved()
        {
            var token = Guid.NewGuid();
            var originalType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            var originalConnectorCached = s_systemConnectorField.GetValue(null);

            try
            {
                ClientInfo.ApplyLoginResult(new LoginResponse
                {
                    AccessToken = token,
                    UserId = "u1",
                    UserName = "U1"
                });

                var connector = ClientInfo.SystemApiConnector;
                Assert.NotNull(connector);

                ClientInfo.ApplyLoginResult(new LoginResponse
                {
                    AccessToken = token,
                    UserId = "u1",
                    UserName = "U1"
                });

                var connectorAfter = s_systemConnectorField.GetValue(null);
                Assert.Same(connector, (SystemApiConnector?)connectorAfter);
            }
            finally
            {
                ClientInfo.ApplyLoginResult(new LoginResponse { AccessToken = Guid.Empty });
                ApiClientInfo.ConnectType = originalType;
                ApiClientInfo.Endpoint = originalEndpoint;
                s_systemConnectorField.SetValue(null, originalConnectorCached);
            }
        }
    }
}
