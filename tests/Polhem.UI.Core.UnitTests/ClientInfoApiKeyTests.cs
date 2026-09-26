using System.ComponentModel;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// The API key seam of <see cref="ClientInfo"/>: reads and writes go through a replaceable <see cref="IApiKeyStorage"/>,
    /// and <see cref="ClientInfo.ApplyApiKey"/> uses the value shipped with the app only as a first-run seed.
    /// That is what lets the key be changed without recompiling the client.
    /// </summary>
    [Collection("ClientInfoState")]
    public class ClientInfoApiKeyTests
    {
        private sealed class FakeApiKeyStorage : IApiKeyStorage
        {
            public string Stored { get; set; } = string.Empty;
            public int SaveCount { get; private set; }

            public string LoadApiKey() => Stored;

            public void SetApiKey(string apiKey) => Stored = apiKey;

            public void SaveApiKey(string apiKey)
            {
                Stored = apiKey;
                SaveCount++;
            }
        }

        /// <summary>
        /// Runs the test with a replaced storage, then restores both process-wide statics.
        /// </summary>
        private static void WithFakeStorage(Action<FakeApiKeyStorage> action)
        {
            var originalStorage = ClientInfo.ApiKeyStorage;
            var originalKey = ApiClientInfo.ApiKey;
            var fake = new FakeApiKeyStorage();
            ClientInfo.ApiKeyStorage = fake;
            try
            {
                action(fake);
            }
            finally
            {
                ClientInfo.ApiKeyStorage = originalStorage;
                ApiClientInfo.ApiKey = originalKey;
            }
        }

        [Fact]
        [DisplayName("GetApiKey delegates to ApiKeyStorage")]
        public void GetApiKey_DelegatesToStorage()
        {
            WithFakeStorage(fake =>
            {
                fake.Stored = "app-id.secret";

                Assert.Equal("app-id.secret", ClientInfo.GetApiKey());
            });
        }

        [Fact]
        [DisplayName("SetApiKey persists the key and applies it to subsequent API calls immediately")]
        public void SetApiKey_PersistsAndApplies()
        {
            WithFakeStorage(fake =>
            {
                ClientInfo.SetApiKey("new-app.secret");

                Assert.Equal("new-app.secret", fake.Stored);
                Assert.Equal(1, fake.SaveCount);
                Assert.Equal("new-app.secret", ApiClientInfo.ApiKey);
            });
        }

        [Fact]
        [DisplayName("ApplyApiKey seeds and saves the shipped value when the storage is empty")]
        public void ApplyApiKey_EmptyStorage_SeedsWithDefault()
        {
            WithFakeStorage(fake =>
            {
                ClientInfo.ApplyApiKey("shipped-default");

                Assert.Equal("shipped-default", fake.Stored);
                Assert.Equal(1, fake.SaveCount);
                Assert.Equal("shipped-default", ApiClientInfo.ApiKey);
            });
        }

        [Fact]
        [DisplayName("ApplyApiKey keeps the stored value when one exists and the shipped value does not overwrite it")]
        public void ApplyApiKey_ExistingStorage_KeepsStoredValue()
        {
            WithFakeStorage(fake =>
            {
                fake.Stored = "configured.secret";

                ClientInfo.ApplyApiKey("shipped-default");

                Assert.Equal("configured.secret", fake.Stored);
                // An existing value is not written again. This is what keeps a changed key from being reverted on the next start.
                Assert.Equal(0, fake.SaveCount);
                Assert.Equal("configured.secret", ApiClientInfo.ApiKey);
            });
        }

        [Fact]
        [DisplayName("ApplyApiKey applies an empty string without saving when there is no shipped value and the storage is empty")]
        public void ApplyApiKey_NoDefaultAndEmptyStorage_AppliesEmpty()
        {
            WithFakeStorage(fake =>
            {
                ApiClientInfo.ApiKey = "stale";

                ClientInfo.ApplyApiKey();

                Assert.Equal(string.Empty, ApiClientInfo.ApiKey);
                Assert.Equal(0, fake.SaveCount);
            });
        }
    }
}
