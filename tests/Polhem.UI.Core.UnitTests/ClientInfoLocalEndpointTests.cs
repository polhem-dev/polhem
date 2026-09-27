using System.ComponentModel;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers the deep paths of <c>InitializeConnectAsync</c> and <c>SetEndpointAsync</c> in <see cref="ClientInfo"/>
    /// that a valid local endpoint path reaches.
    /// <para>
    /// Both methods call <c>SetConnectType</c> and then <c>await SystemApiConnector.InitializeAsync()</c> only after the
    /// endpoint passes <c>ApiConnectValidator.ValidateAsync</c>. The other tests only use an empty endpoint, which makes
    /// validation throw, so those two steps never ran. These tests pass validation with a temporary directory containing
    /// <c>SystemSettings.xml</c> and cover both steps (<c>InitializeAsync</c> still throws without a local API service,
    /// but <c>SetConnectType</c> has already completed. <c>return true;</c> and <c>SaveEndpoint</c> need a running
    /// API service and remain not coverable in CI).
    /// </para>
    /// It mutates static state, so it shares the collection with the other ClientInfoState tests to run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoLocalEndpointTests
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
            public Task<bool> ShowApiConnectAsync() => Task.FromResult(false);
        }

        private static string CreateTempDefinePath()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"polhem-ci-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            // `ApiConnectValidator.ValidateLocal` only checks that the file exists, not its content.
            File.WriteAllText(Path.Combine(tempDir, "SystemSettings.xml"), "<SystemSettings />");
            return tempDir;
        }

        [Fact]
        [DisplayName("InitializeAsync(IUIViewService) sets ConnectType to Local after a valid local path passes validation")]
        public async Task InitializeAsync_ValidLocalPath_SetsConnectTypeToLocal()
        {
            var tempDir = CreateTempDefinePath();
            var originalStorage = ClientInfo.EndpointStorage;
            var originalConnectType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            var originalSupportedTypes = ApiClientInfo.SupportedConnectTypes;
            try
            {
                ClientInfo.EndpointStorage = new FakeEndpointStorage(tempDir);
                // `InitializeConnectAsync` sets `SupportedConnectTypes` to `Both`. After `ValidateAsync` passes it calls
                // `SetConnectType(Local, tempDir)` and then `SystemApiConnector.InitializeAsync()`. Without a local API
                // service `InitializeAsync` throws, and the catch returns false.
                await ClientInfo.InitializeAsync(new FakeUIViewService(), SupportedConnectTypes.Both);
                Assert.Equal(ConnectType.Local, ApiClientInfo.ConnectType);
            }
            finally
            {
                ClientInfo.EndpointStorage = originalStorage;
                ApiClientInfo.ConnectType = originalConnectType;
                ApiClientInfo.Endpoint = originalEndpoint;
                ApiClientInfo.SupportedConnectTypes = originalSupportedTypes;
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }

        [Fact]
        [DisplayName("SetEndpointAsync sets ConnectType to Local after a valid local path passes validation")]
        public async Task SetEndpointAsync_ValidLocalPath_SetsConnectTypeToLocal()
        {
            var tempDir = CreateTempDefinePath();
            var originalConnectType = ApiClientInfo.ConnectType;
            var originalEndpoint = ApiClientInfo.Endpoint;
            var originalSupportedTypes = ApiClientInfo.SupportedConnectTypes;
            try
            {
                // `SetEndpointAsync` does not set `SupportedConnectTypes` itself, so Local must be supported before the call.
                ApiClientInfo.SupportedConnectTypes = SupportedConnectTypes.Both;
                // After `ValidateAsync` passes it calls `SetConnectType(Local, tempDir)` and then `SystemApiConnector.InitializeAsync()`.
                // Without a local API service that throws and propagates, so `SaveEndpoint` never runs.
                await Record.ExceptionAsync(() => ClientInfo.SetEndpointAsync(tempDir));
                Assert.Equal(ConnectType.Local, ApiClientInfo.ConnectType);
            }
            finally
            {
                ApiClientInfo.ConnectType = originalConnectType;
                ApiClientInfo.Endpoint = originalEndpoint;
                ApiClientInfo.SupportedConnectTypes = originalSupportedTypes;
                try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
            }
        }
    }
}
