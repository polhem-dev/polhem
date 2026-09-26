using System.ComponentModel;
using System.Data;
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
    /// An end-to-end round-trip through <see cref="JsonRpcExecutor"/>: confirms that the <c>RowId</c> of
    /// <c>Employee.GetData</c> is copied to <c>GetDataArgs</c> by ApiInputConverter, that the DataSet returned by the
    /// stub is copied back into the wire response by ApiOutputConverter, and that the framework invariant
    /// <c>DataSetName == ProgId</c> holds.
    /// </summary>
    public class GetDataJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public GetDataJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.GetData through JsonRpcExecutor passes RowId through and returns the full DataSet")]
        public void GetData_ThroughJsonRpc_PreservesRowIdAndReturnsDataSet()
        {
            var rowId = Guid.NewGuid();
            var dataSet = new DataSet("Employee");
            var master = new DataTable("Employee");
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add(SysFields.Name, typeof(string));
            master.Rows.Add(rowId, "員工甲");
            dataSet.Tables.Add(master);
            dataSet.AcceptChanges();

            var stub = new StubCrudDataFormRepository { GetDataResult = dataSet };
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
                Method = $"Employee.{FormActions.GetData}",
                Params = new JsonRpcParams { Value = new GetDataRequest { RowId = rowId } },
                Id = Guid.NewGuid().ToString(),
            };

            var response = executor.Execute(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<GetDataResponse>(response.Result!.Value);
            Assert.NotNull(result.DataSet);
            // Framework invariant: `DataSet.DataSetName` equals the ProgId, and `Tables[ProgId]` is the master table.
            Assert.Equal("Employee", result.DataSet!.DataSetName);
            Assert.Equal("員工甲", result.DataSet.Tables["Employee"]!.Rows[0][SysFields.Name]);

            Assert.Equal(rowId, stub.LastRowId);
        }
    }
}
