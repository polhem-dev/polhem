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
    /// 走 <see cref="JsonRpcExecutor"/> 的 end-to-end round-trip:確認
    /// <c>Employee.Delete</c> 的 <c>DeleteRequest.RowId</c> 經 ApiInputConverter
    /// 對拷到 <c>DeleteArgs.RowId</c>,stub 回傳的 <c>RowsAffected</c> 經
    /// ApiOutputConverter 對拷回 wire response。
    /// </summary>
    public class DeleteJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public DeleteJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.Delete 經 JsonRpcExecutor 應透傳 RowId 並回傳 RowsAffected")]
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
