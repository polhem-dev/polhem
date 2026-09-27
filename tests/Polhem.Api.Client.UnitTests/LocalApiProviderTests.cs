using System.ComponentModel;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.JsonRpc;
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
        [DisplayName("ExecuteAsync throws InvalidOperationException naming JsonRpcExecutor when the provider does not register it")]
        public async Task ExecuteAsync_ExecutorNotRegistered_ThrowsInvalidOperationException()
        {
            var provider = new LocalApiProvider(EmptyServiceProvider.Instance, Guid.Empty);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.ExecuteAsync(new JsonRpcRequest()));

            Assert.Contains(nameof(JsonRpcExecutor), ex.Message, StringComparison.Ordinal);
        }
    }
}
