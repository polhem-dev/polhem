using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.JsonRpc.Payload;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for the constructors and argument validation of <see cref="FormApiConnector"/>.
    /// </summary>
    public class FormApiConnectorTests
    {
        private const string TestProgId = "Employee";

        [Fact]
        [DisplayName("FormApiConnector constructor sets ProgId and the client it calls with")]
        public void Constructor_SetsProgIdAndClient()
        {
            var client = PolhemApiClient.CreateRemote("http://example.com/api", string.Empty);
            var connector = new FormApiConnector(client, TestProgId);

            Assert.Equal(TestProgId, connector.ProgId);
            Assert.Same(client, connector.Client);
        }

        [Fact]
        [DisplayName("FormApiConnector constructor throws ArgumentNullException for a null client")]
        public void Constructor_NullClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FormApiConnector(null!, TestProgId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [DisplayName("FormApiConnector.ExecuteAsync throws ArgumentException for an empty action")]
        public async Task ExecuteAsync_EmptyAction_ThrowsArgumentException(string? action)
        {
            var connector = new FormApiConnector(TestClients.Local(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid()), TestProgId);
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await connector.ExecuteAsync<object>(action!, new object(), PayloadFormat.Plain));
        }

        [Fact]
        [DisplayName("FormApiConnector.SaveAsync throws ArgumentNullException for a null DataSet")]
        public async Task SaveAsync_NullDataSet_ThrowsArgumentNullException()
        {
            var connector = new FormApiConnector(TestClients.Local(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid()), TestProgId);
            await Assert.ThrowsAsync<ArgumentNullException>(() => connector.SaveAsync(null!));
        }

        [Fact]
        [DisplayName("FormApiConnector CRUD async methods have no synchronous counterpart (async-only convention)")]
        public void CrudAsyncMethods_HaveNoSyncCounterpart()
        {
            var type = typeof(FormApiConnector);

            // Convention: the CRUD actions are exposed only as async methods, with no synchronous wrappers.
            // This avoids the sync-over-async anti-pattern, which ties up thread pool threads under Blazor Server.
            string[] crudAsyncNames = { "GetNewDataAsync", "GetDataAsync", "SaveAsync", "DeleteAsync" };
            foreach (var asyncName in crudAsyncNames)
            {
                Assert.NotNull(type.GetMethod(asyncName));

                var syncName = asyncName.Replace("Async", string.Empty, StringComparison.Ordinal);
                Assert.Null(type.GetMethod(syncName));
            }
        }
    }
}
