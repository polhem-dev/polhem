using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.JsonRpc.Payload;
using Polhem.Tests.Shared;
using Polhem.Definition.Database;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Uses <see cref="SharedDbFixture"/> to trigger the initialization of
    /// <c>TestProcessBootstrap.LocalServices</c> and SharedDatabaseState. The local-mode [DbFact] tests go through
    /// an in-process client to resolve the backend JsonRpcDispatcher, and complete the CreateSession flow on SQL Server.
    /// </summary>
    public class SystemApiConnectorTests : IClassFixture<SharedDbFixture>
    {
        public SystemApiConnectorTests(SharedDbFixture _)
        {
            // The fixture only triggers the TestProcessBootstrap and SharedDatabaseState initialization. The test methods
            // pass the process-wide `TestProcessBootstrap.LocalServices` to the local clients.
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

            // A random access token is only needed to sign the local client in. `CreateSession` returns a new token.
            Guid accessToken = Guid.NewGuid();
            var connector = new SystemApiConnector(TestClients.Local(Polhem.Tests.Shared.TestProcessBootstrap.LocalServices, accessToken));

            // Act
            var response = await connector.CreateSessionAsync(userId, expiresIn);

            // Assert
            Assert.NotEqual(Guid.Empty, response.AccessToken);
        }

        [Fact]
        [DisplayName("SystemApiConnector constructor sets the client it calls with")]
        public void Constructor_SetsClient()
        {
            var client = TestClients.Local(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid());
            var connector = new SystemApiConnector(client);

            Assert.Same(client, connector.Client);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [DisplayName("SystemApiConnector.ExecuteAsync throws ArgumentException for an empty action")]
        public async Task ExecuteAsync_EmptyAction_ThrowsArgumentException(string? action)
        {
            var connector = new ExposedSystemApiConnector(
                TestClients.Local(Polhem.Tests.Shared.TestProcessBootstrap.LocalServices, Guid.NewGuid()));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await connector.CallAsync<object>(action!, new object(), PayloadFormat.Plain));
        }

        /// <summary>
        /// Reaches the protected <c>ExecuteAsync</c> the way a host's own connector subclass would.
        /// </summary>
        private sealed class ExposedSystemApiConnector(PolhemApiClient client) : SystemApiConnector(client)
        {
            public Task<T> CallAsync<T>(string action, object value, PayloadFormat format)
                => ExecuteAsync<T>(action, value, format);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SystemApiConnector.PingAsync succeeds over a local connection")]
        public async Task PingAsync_LocalConnector_Succeeds()
        {
            var connector = new SystemApiConnector(TestClients.Local(Polhem.Tests.Shared.TestProcessBootstrap.LocalServices, Guid.NewGuid()));
            var exception = await Record.ExceptionAsync(() => connector.PingAsync());
            Assert.Null(exception);
        }

    }
}
