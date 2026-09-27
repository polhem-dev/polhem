using System.ComponentModel;
using Polhem.Api.Client.Providers;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Pure logic tests for the constructors and argument validation of <see cref="FormApiConnector"/>.
    /// </summary>
    public class FormApiConnectorTests
    {
        private const string TestProgId = "Employee";

        [Fact]
        [DisplayName("FormApiConnector local constructor sets ProgId and a LocalApiProvider")]
        public void Constructor_Local_SetsProgIdAndProvider()
        {
            var token = Guid.NewGuid();
            var connector = new FormApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, token, TestProgId);

            Assert.Equal(token, connector.AccessToken);
            Assert.Equal(TestProgId, connector.ProgId);
            Assert.IsType<LocalApiProvider>(connector.Provider);
        }

        [Fact]
        [DisplayName("FormApiConnector remote constructor sets ProgId and a RemoteApiProvider")]
        public void Constructor_Remote_SetsProgIdAndProvider()
        {
            var token = Guid.NewGuid();
            var connector = new FormApiConnector("http://example.com/api", token, TestProgId);

            Assert.Equal(token, connector.AccessToken);
            Assert.Equal(TestProgId, connector.ProgId);
            Assert.IsType<RemoteApiProvider>(connector.Provider);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("FormApiConnector remote constructor throws ArgumentException for a blank endpoint")]
        public void Constructor_RemoteEmptyEndpoint_ThrowsArgumentException(string? endpoint)
        {
            Assert.Throws<ArgumentException>(() => new FormApiConnector(endpoint!, Guid.NewGuid(), TestProgId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [DisplayName("FormApiConnector.ExecuteAsync throws ArgumentException for an empty action")]
        public async Task ExecuteAsync_EmptyAction_ThrowsArgumentException(string? action)
        {
            var connector = new FormApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), TestProgId);
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await connector.ExecuteAsync<object>(action!, new object(), PayloadFormat.Plain));
        }

        [Fact]
        [DisplayName("FormApiConnector.SaveAsync throws ArgumentNullException for a null DataSet")]
        public async Task SaveAsync_NullDataSet_ThrowsArgumentNullException()
        {
            var connector = new FormApiConnector(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid(), TestProgId);
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
