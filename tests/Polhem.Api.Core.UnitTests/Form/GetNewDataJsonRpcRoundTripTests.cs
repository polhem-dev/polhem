using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.JsonRpc;
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
    /// An end-to-end round-trip through the JSON-RPC dispatcher: <c>Employee.GetNewData</c> is dispatched by the
    /// dispatcher to the BO, and a stub repository returns a known skeleton DataSet. It verifies the name-convention
    /// copy and the DataSet restore.
    /// </summary>
    public class GetNewDataJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public GetNewDataJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.GetNewData through the JSON-RPC dispatcher dispatches to the BO and returns the stub skeleton DataSet")]
        public async Task GetNewData_ThroughJsonRpc_DispatchesAndReturnsDataSet()
        {
            var skeleton = new DataSet("Employee");
            var master = new DataTable("Employee");
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add(SysFields.Name, typeof(string));
            var skeletonRowId = Guid.NewGuid();
            master.Rows.Add(skeletonRowId, "預設員工");
            skeleton.Tables.Add(master);

            var stub = new StubCrudDataFormRepository { GetNewDataResult = skeleton };
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
                Method = $"Employee.{FormActions.GetNewData}",
                Params = new TestPayload { Value = new GetNewDataRequest() },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetNewDataResponse>(response.Result!.Value);
            Assert.NotNull(result.DataSet);
            // Framework invariant: `DataSet.DataSetName` equals the ProgId, and `Tables[ProgId]` is the master table.
            Assert.Equal("Employee", result.DataSet!.DataSetName);
            Assert.Equal(skeletonRowId, (Guid)result.DataSet.Tables["Employee"]!.Rows[0][SysFields.RowId]);

            Assert.True(stub.GetNewDataCalled);
        }

        [Fact]
        [DisplayName("A form method on a progId with no stored schema returns the UserMessage code and names the progId")]
        public async Task GetNewData_UnknownProgId_ReturnsDefinitionNotFoundMessage()
        {
            var stub = new StubCrudDataFormRepository { GetNewDataResult = new DataSet() };
            var overrideServices = new TestOverrideServiceProvider(
                _fx.Provider,
                (typeof(IRepositoryFactory), new StubCrudFormRepositoryFactory(stub)));

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

            string progId = "NoSuchForm" + Guid.NewGuid().ToString("N");
            var request = new TestRpcRequest
            {
                Method = $"{progId}.{FormActions.GetNewData}",
                Params = new TestPayload { Value = new GetNewDataRequest() },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.NotNull(response.Error);
            Assert.Equal((int)JsonRpcErrorCode.UserMessage, response.Error!.Code);
            Assert.Equal($"FormSchema '{progId}' not found.", response.Error.Message);
        }
    }
}
