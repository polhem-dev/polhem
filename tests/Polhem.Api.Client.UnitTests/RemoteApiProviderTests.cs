using System.ComponentModel;
using System.Net;
using System.Text;
using Polhem.Api.Client.Providers;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for the constructor and properties of <see cref="RemoteApiProvider"/>.
    /// </summary>
    public class RemoteApiProviderTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("RemoteApiProvider constructor throws ArgumentException for a blank endpoint")]
        public void Constructor_NullOrEmptyEndpoint_ThrowsArgumentException(string? endpoint)
        {
            Assert.Throws<ArgumentException>(() => new RemoteApiProvider(endpoint!, Guid.Empty));
        }

        [Fact]
        [DisplayName("RemoteApiProvider constructor sets Endpoint and AccessToken")]
        public void Constructor_ValidArgs_SetsProperties()
        {
            var token = Guid.NewGuid();
            var provider = new RemoteApiProvider("http://example.com/api", token);

            Assert.Equal("http://example.com/api", provider.Endpoint);
            Assert.Equal(token, provider.AccessToken);
        }

        [Fact]
        [DisplayName("RemoteApiProvider constructor accepts Guid.Empty as AccessToken (for Login and Ping)")]
        public void Constructor_EmptyAccessToken_IsAccepted()
        {
            var provider = new RemoteApiProvider("http://example.com/api", Guid.Empty);

            Assert.Equal(Guid.Empty, provider.AccessToken);
        }
    
        [Fact]
        [DisplayName("RemoteApiProvider sends no Authorization header before sign-in")]
        public async Task ExecuteAsync_EmptyAccessToken_SendsNoAuthorizationHeader()
        {
            var handler = new CapturingHandler();
            using var client = new HttpClient(handler);
            var provider = new RemoteApiProvider("http://example.invalid/api", Guid.Empty, client);

            await provider.ExecuteAsync(new JsonRpcRequest { Method = "System.Ping" });

            Assert.NotNull(handler.Request);
            Assert.False(handler.Request!.Headers.Contains(ApiHeaders.Authorization));
        }

        [Fact]
        [DisplayName("RemoteApiProvider sends the access token as a Bearer header after sign-in")]
        public async Task ExecuteAsync_AccessToken_SendsBearerHeader()
        {
            var handler = new CapturingHandler();
            using var client = new HttpClient(handler);
            var token = Guid.NewGuid();
            var provider = new RemoteApiProvider("http://example.invalid/api", token, client);

            await provider.ExecuteAsync(new JsonRpcRequest { Method = "System.Ping" });

            Assert.NotNull(handler.Request);
            Assert.Equal($"Bearer {token}", string.Join(",", handler.Request!.Headers.GetValues(ApiHeaders.Authorization)));
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? Request { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"result\":null}", Encoding.UTF8, "application/json"),
                });
            }
        }
}
}
