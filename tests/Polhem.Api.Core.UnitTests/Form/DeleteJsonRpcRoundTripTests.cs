using System.ComponentModel;
using Polhem.Api.Core.Messages.Form;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Tests.Shared;
using Polhem.Api.Core.UnitTests.Dispatch;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// An end-to-end round-trip through the JSON-RPC dispatcher: confirms that <c>DeleteRequest.RowId</c> of
    /// <c>Employee.Delete</c> is copied to <c>DeleteArgs.RowId</c> by ApiInputConverter, and that the
    /// <c>RowsAffected</c> returned by the stub is copied back to the wire response by ApiOutputConverter.
    /// </summary>
    public class DeleteJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public DeleteJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.Delete through the JSON-RPC dispatcher passes RowId through and returns RowsAffected")]
        public async Task Delete_ThroughJsonRpc_PreservesRowIdAndReturnsRowsAffected()
        {
            var rowId = Guid.NewGuid();
            var stub = new StubCrudDataFormRepository { DeleteResult = 1 };
            var stubFactory = new StubCrudFormRepositoryFactory(stub);

            var overrideServices = new TestOverrideServiceProvider(
                _fx.Provider,
                (typeof(IRepositoryFactory), stubFactory));

            var boFactory = new BusinessObjectFactory(
                overrideServices,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());

            var executor = new TestDispatcher(_fx.Provider, boFactory)
            {
                AccessToken = TestSessionFactory.CreateAccessToken(_fx),
                IsLocalCall = true,
            };

            var request = new TestRpcRequest
            {
                Method = $"Employee.{FormActions.Delete}",
                Params = new TestPayload { Value = new DeleteRequest { RowId = rowId } },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<DeleteResponse>(response.Result!.Value);
            Assert.Equal(1, result.RowsAffected);
            Assert.Equal(rowId, stub.LastRowId);
        }
    }
}
