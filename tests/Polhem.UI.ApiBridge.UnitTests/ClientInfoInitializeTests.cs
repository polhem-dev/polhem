using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;

namespace Polhem.UI.ApiBridge.UnitTests
{
    /// <summary>
    /// Covers <see cref="ClientInfo.InitializeAsync(SupportedConnectTypes,CancellationToken)"/>.
    /// A missing endpoint makes it return <c>false</c> so the host can show its own connection setup; the endpoint
    /// storage is replaced so the result does not depend on what the machine running the tests has stored.
    /// It shares the <c>ClientInfoState</c> collection with the other tests that mutate static state, so they run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoInitializeTests
    {
        private sealed class FakeEndpointStorage : IEndpointStorage
        {
            public string LoadEndpoint() => string.Empty;
            public void SetEndpoint(string endpoint) { }
            public void SaveEndpoint(string endpoint) { }
        }

        private static readonly PropertyInfo s_argumentsProp =
            typeof(ClientInfo).GetProperty("Arguments",
                BindingFlags.Public | BindingFlags.Static)!;

        [Fact]
        [DisplayName("InitializeAsync(SupportedConnectTypes) returns false when no endpoint is stored")]
        public async Task InitializeAsync_NoStoredEndpoint_ReturnsFalse()
        {
            var originalStorage = ClientInfo.EndpointStorage;
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            var originalArgs = ClientInfo.Arguments;
            try
            {
                ClientInfo.EndpointStorage = new FakeEndpointStorage();
                var result = await ClientInfo.InitializeAsync(SupportedConnectTypes.Both);
                Assert.False(result);
            }
            finally
            {
                ClientInfo.EndpointStorage = originalStorage;
                ClientInfo.SupportedConnectTypes = originalSupportedTypes;
                s_argumentsProp.GetSetMethod(nonPublic: true)?.Invoke(null, new object?[] { originalArgs });
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(SupportedConnectTypes) leaves Arguments as a non-null dictionary")]
        public async Task InitializeAsync_SetsArgumentsToNonNull()
        {
            var originalStorage = ClientInfo.EndpointStorage;
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            var originalArgs = ClientInfo.Arguments;
            try
            {
                ClientInfo.EndpointStorage = new FakeEndpointStorage();
                await ClientInfo.InitializeAsync(SupportedConnectTypes.Both);
                Assert.NotNull(ClientInfo.Arguments);
            }
            finally
            {
                ClientInfo.EndpointStorage = originalStorage;
                ClientInfo.SupportedConnectTypes = originalSupportedTypes;
                s_argumentsProp.GetSetMethod(nonPublic: true)?.Invoke(null, new object?[] { originalArgs });
            }
        }
    }

    /// <summary>
    /// Covers <see cref="ClientInfo.InitializeAsync(string,CancellationToken)"/>.
    /// An empty endpoint fails before <see cref="ApiConnectValidator.ValidateAsync"/>
    /// and mutates no static state, so no serializing collection is needed.
    /// </summary>
    public class ClientInfoStringEndpointTests
    {
        [Fact]
        [DisplayName("InitializeAsync(string) throws ArgumentException for an empty endpoint")]
        public async Task InitializeAsync_EmptyStringEndpoint_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => ClientInfo.InitializeAsync(string.Empty));
        }
    }
}
