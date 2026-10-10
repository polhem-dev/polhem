using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Client;

namespace Polhem.UI.Core.UnitTests
{
    /// <summary>
    /// Covers <see cref="ClientInfo.InitializeAsync(IUIViewService,SupportedConnectTypes,CancellationToken)"/>.
    /// When the endpoint is invalid, this method calls <see cref="IUIViewService.ShowApiConnectAsync"/>.
    /// A lightweight fake replaces the real UI service, so the try-catch path is covered without a backend.
    /// It shares the <c>ClientInfoState</c> collection with the other tests that mutate static state, so they run serially.
    /// </summary>
    [Collection(ClientInfoStateCollection.Name)]
    public class ClientInfoInitializeTests
    {
        private sealed class FakeUIViewService : IUIViewService
        {
            private readonly bool _result;
            public FakeUIViewService(bool result) { _result = result; }
            public Task<bool> ShowApiConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(_result);
        }

        private static readonly PropertyInfo s_uiViewServiceProp =
            typeof(ClientInfo).GetProperty("UIViewService",
                BindingFlags.Public | BindingFlags.Static)!;

        private static readonly PropertyInfo s_argumentsProp =
            typeof(ClientInfo).GetProperty("Arguments",
                BindingFlags.Public | BindingFlags.Static)!;

        private static void RestoreState(
            SupportedConnectTypes originalSupportedTypes,
            IUIViewService? originalViewService,
            IReadOnlyDictionary<string, string>? originalArgs)
        {
            ClientInfo.SupportedConnectTypes = originalSupportedTypes;
            s_uiViewServiceProp.GetSetMethod(nonPublic: true)?.Invoke(null, new object?[] { originalViewService });
            s_argumentsProp.GetSetMethod(nonPublic: true)?.Invoke(null, new object?[] { originalArgs });
        }

        [Fact]
        [DisplayName("InitializeAsync(IUIViewService) returns false when ShowApiConnectAsync returns false")]
        public async Task InitializeAsync_ShowApiConnectReturnsFalse_ReturnsFalse()
        {
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            var originalViewService = ClientInfo.UIViewService;
            var originalArgs = ClientInfo.Arguments;
            try
            {
                var result = await ClientInfo.InitializeAsync(new FakeUIViewService(false), SupportedConnectTypes.Both);
                Assert.False(result);
            }
            finally
            {
                RestoreState(originalSupportedTypes, originalViewService, originalArgs);
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(IUIViewService) returns true when ShowApiConnectAsync returns true")]
        public async Task InitializeAsync_ShowApiConnectReturnsTrue_ReturnsTrue()
        {
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            var originalViewService = ClientInfo.UIViewService;
            var originalArgs = ClientInfo.Arguments;
            try
            {
                var result = await ClientInfo.InitializeAsync(new FakeUIViewService(true), SupportedConnectTypes.Both);
                Assert.True(result);
            }
            finally
            {
                RestoreState(originalSupportedTypes, originalViewService, originalArgs);
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(IUIViewService) sets UIViewService to the passed service instance")]
        public async Task InitializeAsync_SetsUIViewServiceToPassedInstance()
        {
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            var originalViewService = ClientInfo.UIViewService;
            var originalArgs = ClientInfo.Arguments;
            try
            {
                var service = new FakeUIViewService(true);
                await ClientInfo.InitializeAsync(service, SupportedConnectTypes.Both);
                Assert.Same(service, ClientInfo.UIViewService);
            }
            finally
            {
                RestoreState(originalSupportedTypes, originalViewService, originalArgs);
            }
        }

        [Fact]
        [DisplayName("InitializeAsync(IUIViewService) leaves Arguments as a non-null dictionary")]
        public async Task InitializeAsync_SetsArgumentsToNonNull()
        {
            var originalSupportedTypes = ClientInfo.SupportedConnectTypes;
            var originalViewService = ClientInfo.UIViewService;
            var originalArgs = ClientInfo.Arguments;
            try
            {
                await ClientInfo.InitializeAsync(new FakeUIViewService(true), SupportedConnectTypes.Both);
                Assert.NotNull(ClientInfo.Arguments);
            }
            finally
            {
                RestoreState(originalSupportedTypes, originalViewService, originalArgs);
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
