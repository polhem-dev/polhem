using System.ComponentModel;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
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

        private sealed class FakeUIViewService : IUIViewService
        {
            private readonly bool _result;
            public FakeUIViewService(bool result) => _result = result;
            public Task<bool> ShowApiConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(_result);
        }

        [Fact]
        [DisplayName("InitializeAsync(IUIViewService) returns false for an unreachable remote URL endpoint")]
        public async Task InitializeAsync_UnreachableRemoteUrl_ReturnsFalse()
        {
            var originalStorage = ClientInfo.EndpointStorage;
            var originalSupportedTypes = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                ClientInfo.EndpointStorage = new FakeEndpointStorage("http://localhost:19999");
                var result = await ClientInfo.InitializeAsync(new FakeUIViewService(false), SupportedConnectTypes.Both);
                Assert.False(result);
            }
            finally
            {
                ClientInfo.EndpointStorage = originalStorage;
                ApiClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(IUIViewService) with a cancelled token throws instead of opening the connection setup")]
        public async Task InitializeAsync_CancelledToken_ThrowsWithoutShowingSetup()
        {
            var originalStorage = ClientInfo.EndpointStorage;
            var originalSupportedTypes = ApiClientInfo.SupportedConnectTypes;
            var service = new CountingUIViewService();
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                ClientInfo.EndpointStorage = new FakeEndpointStorage("http://localhost:19999");

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => ClientInfo.InitializeAsync(service, SupportedConnectTypes.Both, cancellation.Token));
                Assert.Equal(0, service.ShowCount);
            }
            finally
            {
                ClientInfo.EndpointStorage = originalStorage;
                ApiClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }

        private sealed class CountingUIViewService : IUIViewService
        {
            public int ShowCount { get; private set; }

            public Task<bool> ShowApiConnectAsync(CancellationToken cancellationToken = default)
            {
                ShowCount++;
                return Task.FromResult(false);
            }
        }

        [Fact]
        [DisplayName("SetEndpointAsync throws InvalidOperationException for an unreachable remote URL")]
        public async Task SetEndpointAsync_UnreachableRemoteUrl_ThrowsInvalidOperationException()
        {
            var originalSupportedTypes = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                var ex = await Record.ExceptionAsync(() => ClientInfo.SetEndpointAsync("http://localhost:19999"));
                Assert.IsType<InvalidOperationException>(ex);
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(string) throws InvalidOperationException for an unreachable remote URL")]
        public async Task InitializeAsync_UnreachableRemoteUrl_ThrowsInvalidOperationException()
        {
            var originalSupportedTypes = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                var ex = await Record.ExceptionAsync(() => ClientInfo.InitializeAsync("http://localhost:19999"));
                Assert.IsType<InvalidOperationException>(ex);
            }
            finally
            {
                ApiClientInfo.SupportedConnectTypes = originalSupportedTypes;
            }
        }
    }
}
