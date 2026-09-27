using System.ComponentModel;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for the static properties of <see cref="ApiClientInfo"/>. Like <c>ApiConnectValidatorTests</c>
    /// it mutates the process-wide static <c>ApiClientInfo.SupportedConnectTypes</c>, so both are in
    /// <c>[Collection(ApiClientInfoStateCollection.Name)]</c> and run serially, avoiding races between parallel classes.
    /// </summary>
    [Collection(ApiClientInfoStateCollection.Name)]
    public class ApiClientInfoTests
    {
        /// <summary>
        /// Backs up the static state before the test and restores it afterwards, so tests do not pollute each other.
        /// </summary>
        private static void WithSnapshot(Action action)
        {
            var supported = ApiClientInfo.SupportedConnectTypes;
            var connectType = ApiClientInfo.ConnectType;
            var endpoint = ApiClientInfo.Endpoint;
            var apiKey = ApiClientInfo.ApiKey;
            try
            {
                action();
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = supported;
                ApiClientInfo.ConnectType = connectType;
                ApiClientInfo.Endpoint = endpoint;
                ApiClientInfo.ApiKey = apiKey;
            }
        }

        [Fact]
        [DisplayName("ApiClientInfo defaults match the design")]
        public void Defaults_AreExpected()
        {
            WithSnapshot(() =>
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                ApiClientInfo.ConnectType = ConnectType.Local;
                ApiClientInfo.Endpoint = string.Empty;
                ApiClientInfo.ApiKey = string.Empty;

                Assert.Equal(SupportedConnectTypes.Both, ApiClientInfo.SupportedConnectTypes);
                Assert.Equal(ConnectType.Local, ApiClientInfo.ConnectType);
                Assert.Equal(string.Empty, ApiClientInfo.Endpoint);
                Assert.Equal(string.Empty, ApiClientInfo.ApiKey);
            });
        }

        [Fact]
        [DisplayName("ApiClientInfo.SupportedConnectTypes can be overwritten and read back")]
        public void SupportedConnectTypes_CanBeOverwritten()
        {
            WithSnapshot(() =>
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Remote;
                Assert.Equal(SupportedConnectTypes.Remote, ApiClientInfo.SupportedConnectTypes);

                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Local;
                Assert.Equal(SupportedConnectTypes.Local, ApiClientInfo.SupportedConnectTypes);
            });
        }

        [Fact]
        [DisplayName("ApiClientInfo.ConnectType can be overwritten and read back")]
        public void ConnectType_CanBeOverwritten()
        {
            WithSnapshot(() =>
            {
                ApiClientInfo.ConnectType = ConnectType.Remote;
                Assert.Equal(ConnectType.Remote, ApiClientInfo.ConnectType);
            });
        }

        [Fact]
        [DisplayName("ApiClientInfo.Endpoint and ApiKey can be overwritten and read back")]
        public void EndpointAndApiKey_CanBeOverwritten()
        {
            WithSnapshot(() =>
            {
                ApiClientInfo.Endpoint = "http://example.com";
                ApiClientInfo.ApiKey = "test-api-key";

                Assert.Equal("http://example.com", ApiClientInfo.Endpoint);
                Assert.Equal("test-api-key", ApiClientInfo.ApiKey);
            });
        }

        [Fact]
        [DisplayName("SupportedConnectTypes.Both equals Local OR Remote")]
        public void SupportedConnectTypes_Both_EqualsLocalOrRemote()
        {
            Assert.Equal(SupportedConnectTypes.Local | SupportedConnectTypes.Remote, SupportedConnectTypes.Both);
            Assert.True(SupportedConnectTypes.Both.HasFlag(SupportedConnectTypes.Local));
            Assert.True(SupportedConnectTypes.Both.HasFlag(SupportedConnectTypes.Remote));
        }
    }
}
