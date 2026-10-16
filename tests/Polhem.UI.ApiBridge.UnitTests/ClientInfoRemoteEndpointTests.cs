using System.ComponentModel;
using Polhem.Api.Client;

namespace Polhem.UI.ApiBridge.UnitTests
{
    /// <summary>
    /// Verifies how <see cref="ClientInfo"/> guards against a remote endpoint that cannot be reached.
    /// <para>
    /// <c>ApiConnectValidator.ValidateRemoteAsync</c> calls <c>PingAsync</c>, and reports a host that cannot be
    /// reached as an unreachable endpoint. If the ping fails, the exception is thrown before <c>SetConnectType</c> is called.
    /// This class verifies only that throwing behavior and how <c>InitializeConnectAsync</c> swallows the exception.
    /// </para>
    /// It mutates static state, so it shares the collection with the other ClientInfoState tests to run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoRemoteEndpointTests
    {
        private sealed class FakeEndpointStorage : IEndpointStorage
        {
            private readonly string _endpoint;
            public FakeEndpointStorage(string endpoint) => _endpoint = endpoint;
            public string LoadEndpoint() => _endpoint;
            public void SetEndpoint(string endpoint) { }
            public void SaveEndpoint(string endpoint) { }
        }

        [Fact]
        [DisplayName("InitializeAsync(SupportedConnectTypes) returns false for an unreachable remote URL endpoint")]
        public async Task InitializeAsync_UnreachableRemoteUrl_ReturnsFalse()
        {
            var originalStorage = ClientInfo.EndpointStorage;
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            try
            {
                ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                ClientInfo.EndpointStorage = new FakeEndpointStorage("http://localhost:19999");
                var result = await ClientInfo.InitializeAsync(SupportedConnectTypes.Both);
                Assert.False(result);
            }
            finally
            {
                ClientInfo.EndpointStorage = originalStorage;
                ClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(SupportedConnectTypes) with a cancelled token throws instead of returning false")]
        public async Task InitializeAsync_CancelledToken_Throws()
        {
            var originalStorage = ClientInfo.EndpointStorage;
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            try
            {
                ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                ClientInfo.EndpointStorage = new FakeEndpointStorage("http://localhost:19999");

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => ClientInfo.InitializeAsync(SupportedConnectTypes.Both, cancellation.Token));
            }
            finally
            {
                ClientInfo.EndpointStorage = originalStorage;
                ClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }

        [Fact]
        [DisplayName("SetEndpointAsync throws InvalidOperationException for an unreachable remote URL")]
        public async Task SetEndpointAsync_UnreachableRemoteUrl_ThrowsInvalidOperationException()
        {
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            try
            {
                ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                var ex = await Record.ExceptionAsync(() => ClientInfo.SetEndpointAsync("http://localhost:19999"));
                Assert.IsType<InvalidOperationException>(ex);
            }
            finally
            {
                ClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(string) throws InvalidOperationException for an unreachable remote URL")]
        public async Task InitializeAsync_UnreachableRemoteUrl_ThrowsInvalidOperationException()
        {
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            try
            {
                ClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                var ex = await Record.ExceptionAsync(() => ClientInfo.InitializeAsync("http://localhost:19999"));
                Assert.IsType<InvalidOperationException>(ex);
            }
            finally
            {
                ClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }
    }
}
