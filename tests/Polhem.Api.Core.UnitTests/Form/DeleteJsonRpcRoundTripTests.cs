using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.Form;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Security;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Tests.Shared;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// An end-to-end round-trip through <see cref="JsonRpcExecutor"/>: confirms that <c>DeleteRequest.RowId</c> of
    /// <c>Employee.Delete</c> is copied to <c>DeleteArgs.RowId</c> by ApiInputConverter, and that the
    /// <c>RowsAffected</c> returned by the stub is copied back to the wire response by ApiOutputConverter.
    /// </summary>
    public class DeleteJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public DeleteJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.Delete through JsonRpcExecutor passes RowId through and returns RowsAffected")]
        public void Delete_ThroughJsonRpc_PreservesRowIdAndReturnsRowsAffected()
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

            var executor = new JsonRpcExecutor(
                boFactory,
                _fx.GetRequiredService<IAccessTokenValidator>(),
                _fx.GetRequiredService<IApiEncryptionKeyProvider>())
            {
                AccessToken = TestSessionFactory.CreateAccessToken(_fx),
                IsLocalCall = true,
            };

            var request = new JsonRpcRequest
            {
                Method = $"Employee.{FormActions.Delete}",
                Params = new JsonRpcParams { Value = new DeleteRequest { RowId = rowId } },
                Id = Guid.NewGuid().ToString(),
            };

            var response = executor.Execute(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<DeleteResponse>(response.Result!.Value);
            Assert.Equal(1, result.RowsAffected);
            Assert.Equal(rowId, stub.LastRowId);
        }
    }
}
