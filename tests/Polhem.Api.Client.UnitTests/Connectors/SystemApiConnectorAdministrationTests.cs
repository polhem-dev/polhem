using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition;
using Polhem.Definition.Security;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// The administration calls of <see cref="SystemApiConnector"/>: API keys, the deployment administrator flag and
    /// the customization plug-in settings. Each test pins the method sent, the request fields and the response read.
    /// </summary>
    public class SystemApiConnectorAdministrationTests
    {
        private static (SystemApiConnector Connector, FakeApiTransport Transport) Create(object resultValue)
        {
            var transport = new FakeApiTransport(call => FakeApiTransport.Answer(call, resultValue));
            return (new SystemApiConnector(TestClients.Fake(transport)), transport);
        }

        private static string MethodOf(string action) => $"{SysProgIds.System}.{action}";

        [Fact]
        [DisplayName("CreateApiKeyAsync sends CreateApiKey with every request field and returns the issued key")]
        public async Task CreateApiKeyAsync_SendsRequestFields_ReturnsResponse()
        {
            var expiry = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var (connector, transport) = Create(new CreateApiKeyResponse { SysId = "erp-sync", ApiKey = "secret-value" });

            var response = await connector.CreateApiKeyAsync("erp-sync", "ERP sync", ApiKeyType.ThirdParty, "ops@example.com", expiry);

            Assert.Equal(MethodOf(SystemActions.CreateApiKey), transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<CreateApiKeyRequest>();
            Assert.Equal("erp-sync", sent.SysId);
            Assert.Equal("ERP sync", sent.SysName);
            Assert.Equal(ApiKeyType.ThirdParty, sent.KeyType);
            Assert.Equal("ops@example.com", sent.Contact);
            Assert.Equal(expiry, sent.ExpiredAt);
            Assert.Equal("secret-value", response.ApiKey);
        }

        [Fact]
        [DisplayName("ListApiKeysAsync sends ListApiKeys and returns the listed keys")]
        public async Task ListApiKeysAsync_SendsAction_ReturnsKeys()
        {
            var listed = new ListApiKeysResponse();
            listed.ApiKeys.Add(new ApiKeySummary { SysId = "erp-sync" });
            var (connector, transport) = Create(listed);

            var response = await connector.ListApiKeysAsync();

            Assert.Equal(MethodOf(SystemActions.ListApiKeys), transport.LastCall!.Method);
            Assert.Equal("erp-sync", Assert.Single(response.ApiKeys).SysId);
        }

        [Fact]
        [DisplayName("SetApiKeyEnabledAsync sends SetApiKeyEnabled with the key id and the flag")]
        public async Task SetApiKeyEnabledAsync_SendsKeyAndFlag()
        {
            var (connector, transport) = Create(new SetApiKeyEnabledResponse { SysId = "erp-sync", Enabled = false });

            var response = await connector.SetApiKeyEnabledAsync("erp-sync", false);

            Assert.Equal(MethodOf(SystemActions.SetApiKeyEnabled), transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<SetApiKeyEnabledRequest>();
            Assert.Equal("erp-sync", sent.SysId);
            Assert.False(sent.Enabled);
            Assert.Equal("erp-sync", response.SysId);
        }

        [Fact]
        [DisplayName("SetApiKeyExpiryAsync sends SetApiKeyExpiry with the key id and the expiry")]
        public async Task SetApiKeyExpiryAsync_SendsKeyAndExpiry()
        {
            var expiry = new DateTime(2031, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var (connector, transport) = Create(new SetApiKeyExpiryResponse { SysId = "erp-sync", ExpiredAt = expiry });

            var response = await connector.SetApiKeyExpiryAsync("erp-sync", expiry);

            Assert.Equal(MethodOf(SystemActions.SetApiKeyExpiry), transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<SetApiKeyExpiryRequest>();
            Assert.Equal("erp-sync", sent.SysId);
            Assert.Equal(expiry, sent.ExpiredAt);
            Assert.Equal(expiry, response.ExpiredAt);
        }

        [Fact]
        [DisplayName("SetDeploymentAdminAsync sends SetDeploymentAdmin with the user id and the flag")]
        public async Task SetDeploymentAdminAsync_SendsUserAndFlag()
        {
            var (connector, transport) = Create(new SetDeploymentAdminResponse { UserId = "admin", IsDeploymentAdmin = true });

            var response = await connector.SetDeploymentAdminAsync("admin", true);

            Assert.Equal(MethodOf(SystemActions.SetDeploymentAdmin), transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<SetDeploymentAdminRequest>();
            Assert.Equal("admin", sent.UserId);
            Assert.True(sent.IsDeploymentAdmin);
            Assert.True(response.IsDeploymentAdmin);
        }

        [Fact]
        [DisplayName("GetCustomizePluginSettingsAsync sends the customize id and returns the stored XML")]
        public async Task GetCustomizePluginSettingsAsync_SendsCustomizeId_ReturnsXml()
        {
            var (connector, transport) = Create(new GetCustomizePluginSettingsResponse { Xml = "<PluginSettings />" });

            var response = await connector.GetCustomizePluginSettingsAsync("tenant-a");

            Assert.Equal(MethodOf(SystemActions.GetCustomizePluginSettings), transport.LastCall!.Method);
            Assert.Equal("tenant-a", transport.LastCall!.SentValue<GetCustomizePluginSettingsRequest>().CustomizeId);
            Assert.Equal("<PluginSettings />", response.Xml);
        }

        [Fact]
        [DisplayName("SaveCustomizePluginSettingsAsync sends the customize id and the XML and returns the plug-in count")]
        public async Task SaveCustomizePluginSettingsAsync_SendsIdAndXml_ReturnsCount()
        {
            var (connector, transport) = Create(new SaveCustomizePluginSettingsResponse { PluginCount = 2 });

            var response = await connector.SaveCustomizePluginSettingsAsync("tenant-a", "<PluginSettings />");

            Assert.Equal(MethodOf(SystemActions.SaveCustomizePluginSettings), transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<SaveCustomizePluginSettingsRequest>();
            Assert.Equal("tenant-a", sent.CustomizeId);
            Assert.Equal("<PluginSettings />", sent.Xml);
            Assert.Equal(2, response.PluginCount);
        }
    }
}
