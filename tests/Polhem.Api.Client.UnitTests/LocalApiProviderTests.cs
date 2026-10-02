using System.ComponentModel;
using Polhem.Api.Client.Providers;
using System.Text.Json;
using Polhem.JsonRpc;
using Polhem.JsonRpc.Server;
using Polhem.Tests.Shared;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for the constructor, properties and dispatch guard of <see cref="LocalApiProvider"/>.
    /// </summary>
    public class LocalApiProviderTests
    {
        [Fact]
        [DisplayName("LocalApiProvider constructor sets AccessToken")]
        public void Constructor_SetsAccessToken()
        {
            var token = Guid.NewGuid();
            var provider = new LocalApiProvider(EmptyServiceProvider.Instance, token);

            Assert.Equal(token, provider.AccessToken);
        }

        [Fact]
        [DisplayName("LocalApiProvider constructor accepts Guid.Empty")]
        public void Constructor_EmptyAccessToken_IsAccepted()
        {
            var provider = new LocalApiProvider(EmptyServiceProvider.Instance, Guid.Empty);

            Assert.Equal(Guid.Empty, provider.AccessToken);
        }

        [Fact]
        [DisplayName("LocalApiProvider constructor throws ArgumentNullException for a null service provider")]
        public void Constructor_NullServices_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LocalApiProvider(null!, Guid.Empty));
        }

        [Fact]
        [DisplayName("SendAsync throws InvalidOperationException naming JsonRpcDispatcher when the provider does not register it")]
        public async Task SendAsync_DispatcherNotRegistered_ThrowsInvalidOperationException()
        {
            var provider = new LocalApiProvider(EmptyServiceProvider.Instance, Guid.Empty);
            var request = new JsonRpcRequest("System.Ping", JsonSerializer.Deserialize<JsonElement>("{}"), JsonRpcId.FromString("1"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SendAsync(request));

            Assert.Contains(nameof(JsonRpcDispatcher), ex.Message, StringComparison.Ordinal);
        }
    }
}
