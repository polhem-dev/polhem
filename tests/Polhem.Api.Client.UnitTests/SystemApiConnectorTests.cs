using System.ComponentModel;
using Polhem.Api.Client.Providers;
using Polhem.Api.Client.Connectors;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Uses <see cref="SharedDbFixture"/> to trigger the GlobalFixture's initialization of
    /// <c>ApiClientInfo.LocalServiceProvider</c> and SharedDatabaseState. The local-mode [DbFact] tests go through
    /// LocalApiProvider to resolve the backend JsonRpcExecutor, and complete the CreateSession flow on SQL Server.
    /// </summary>
    public class SystemApiConnectorTests : IClassFixture<SharedDbFixture>
    {
        public SystemApiConnectorTests(SharedDbFixture _)
        {
            // The fixture only triggers the GlobalFixture and SharedDatabaseState initialization. The test methods
            // use the process-wide `ApiClientInfo.LocalServiceProvider` directly.
        }

        /// <summary>
        /// Tests the CreateSessionAsync method of SystemApiConnector.
        /// </summary>
        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SystemApiConnector CreateSessionAsync returns a valid AccessToken")]
        public async Task CreateSessionAsync_ValidArgs_ReturnsValidToken()
        {
            // Arrange
            string userId = "001";
            int expiresIn = 600;

            // A random access token is only needed to construct the connector. `CreateSession` returns a new token.
            Guid accessToken = Guid.NewGuid();
            var connector = new SystemApiConnector(accessToken);

            // Act
            Guid newToken = await connector.CreateSessionAsync(userId, expiresIn);

            // Assert
            Assert.NotEqual(Guid.Empty, newToken);
        }

        [Fact]
        [DisplayName("SystemApiConnector local constructor creates a LocalApiProvider")]
        public void Constructor_Local_SetsAccessTokenAndLocalProvider()
        {
            var token = Guid.NewGuid();
            var connector = new SystemApiConnector(token);

            Assert.Equal(token, connector.AccessToken);
            Assert.IsType<LocalApiProvider>(connector.Provider);
        }

        [Fact]
        [DisplayName("SystemApiConnector remote constructor creates a RemoteApiProvider")]
        public void Constructor_Remote_SetsAccessTokenAndRemoteProvider()
        {
            var token = Guid.NewGuid();
            var connector = new SystemApiConnector("http://example.com/api", token);

            Assert.Equal(token, connector.AccessToken);
            Assert.IsType<RemoteApiProvider>(connector.Provider);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("SystemApiConnector remote constructor throws ArgumentException for a blank endpoint")]
        public void Constructor_RemoteEmptyEndpoint_ThrowsArgumentException(string? endpoint)
        {
            Assert.Throws<ArgumentException>(() => new SystemApiConnector(endpoint!, Guid.NewGuid()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [DisplayName("SystemApiConnector.ExecuteAsync throws ArgumentException for an empty action")]
        public async Task ExecuteAsync_EmptyAction_ThrowsArgumentException(string? action)
        {
            var connector = new SystemApiConnector(Guid.NewGuid());
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await connector.ExecuteAsync<object>(action!, new object(), PayloadFormat.Plain));
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SystemApiConnector.PingAsync succeeds over a local connection")]
        public async Task PingAsync_LocalConnector_Succeeds()
        {
            var connector = new SystemApiConnector(Guid.NewGuid());
            var exception = await Record.ExceptionAsync(() => connector.PingAsync());
            Assert.Null(exception);
        }

    }
}
