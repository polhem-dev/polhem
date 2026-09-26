using System.ComponentModel;
using Polhem.Api.Client.Providers;

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
    }
}
