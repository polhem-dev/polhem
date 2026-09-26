using System.ComponentModel;
using Polhem.Api.Client.Providers;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for the constructor and properties of <see cref="LocalApiProvider"/>.
    /// </summary>
    public class LocalApiProviderTests
    {
        [Fact]
        [DisplayName("LocalApiProvider constructor sets AccessToken")]
        public void Constructor_SetsAccessToken()
        {
            var token = Guid.NewGuid();
            var provider = new LocalApiProvider(token);

            Assert.Equal(token, provider.AccessToken);
        }

        [Fact]
        [DisplayName("LocalApiProvider constructor accepts Guid.Empty")]
        public void Constructor_EmptyAccessToken_IsAccepted()
        {
            var provider = new LocalApiProvider(Guid.Empty);

            Assert.Equal(Guid.Empty, provider.AccessToken);
        }
    }
}
