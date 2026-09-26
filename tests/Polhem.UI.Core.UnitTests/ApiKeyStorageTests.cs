using System.ComponentModel;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="ApiKeyStorage"/>, symmetric with <see cref="EndpointStorageTests"/>:
    /// the default implementation is backed by <see cref="ClientInfo.ClientSettings"/>.
    /// </summary>
    [Collection("ClientInfoState")]
    public class ApiKeyStorageTests
    {
        [Fact]
        [DisplayName("LoadApiKey reads the key from ClientInfo.ClientSettings.ApiKey")]
        public void LoadApiKey_ReturnsClientSettingsApiKey()
        {
            var storage = new ApiKeyStorage();
            var original = ClientInfo.ClientSettings.ApiKey;
            try
            {
                ClientInfo.ClientSettings.ApiKey = "read-test.secret";
                Assert.Equal("read-test.secret", storage.LoadApiKey());
            }
            finally
            {
                ClientInfo.ClientSettings.ApiKey = original;
            }
        }

        [Fact]
        [DisplayName("SetApiKey updates the value of ClientInfo.ClientSettings.ApiKey")]
        public void SetApiKey_ValidValue_UpdatesClientSettingsApiKey()
        {
            var storage = new ApiKeyStorage();
            var original = ClientInfo.ClientSettings.ApiKey;
            try
            {
                storage.SetApiKey("set-test.secret");
                Assert.Equal("set-test.secret", ClientInfo.ClientSettings.ApiKey);
            }
            finally
            {
                ClientInfo.ClientSettings.ApiKey = original;
            }
        }

        [Fact]
        [DisplayName("SaveApiKey updates ClientInfo.ClientSettings.ApiKey and saves the settings")]
        public void SaveApiKey_ValidValue_UpdatesApiKeyAndSaves()
        {
            var storage = new ApiKeyStorage();
            var original = ClientInfo.ClientSettings.ApiKey;
            try
            {
                storage.SaveApiKey("save-test.secret");
                Assert.Equal("save-test.secret", ClientInfo.ClientSettings.ApiKey);
            }
            finally
            {
                ClientInfo.ClientSettings.ApiKey = original;
            }
        }
    }
}
