using System.ComponentModel;
using System.Data;
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
    /// End-to-end round-trip through the JSON-RPC dispatcher: confirms that the row state of
    /// <c>SaveRequest.DataSet</c> for <c>Employee.Save</c> is kept when ApiInputConverter copies it to
    /// <c>SaveArgs.DataSet</c>, and that the refreshed DataSet and AffectedRows returned by the stub are copied back
    /// to the wire response by ApiOutputConverter.
    /// </summary>
    public class SaveJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public SaveJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.Save through the JSON-RPC dispatcher keeps the RowState and returns the refreshed DataSet and AffectedRows")]
        public async Task Save_ThroughJsonRpc_PreservesRowStatesAndReturnsRefreshed()
        {
            var input = new DataSet("Employee");
            var master = new DataTable("Employee");
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add(SysFields.Name, typeof(string));
            var rowId = Guid.NewGuid();
            master.Rows.Add(rowId, "全新員工");
            input.Tables.Add(master);

            // The refreshed DataSet has a server-generated column, simulating a value written back by a trigger.
            var refreshed = new DataSet("Employee");
            var refreshedMaster = new DataTable("Employee");
            refreshedMaster.Columns.Add(SysFields.RowId, typeof(Guid));
            refreshedMaster.Columns.Add(SysFields.Name, typeof(string));
            refreshedMaster.Columns.Add(SysFields.InsertTime, typeof(DateTime));
            refreshedMaster.Rows.Add(rowId, "全新員工", new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc));
            refreshed.Tables.Add(refreshedMaster);
            refreshed.AcceptChanges();

            var stub = new StubCrudDataFormRepository
            {
                SaveResult = (refreshed, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Employee"] = 1,
                }),
            };
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
                Method = $"Employee.{FormActions.Save}",
                Params = new TestPayload { Value = new SaveRequest { DataSet = input } },
                Id = Guid.NewGuid().ToString(),
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var result = Assert.IsType<SaveResponse>(response.Result!.Value);
            // Framework invariant: the refreshed DataSet also uses the ProgId as its DataSetName.
            Assert.NotNull(result.DataSet);
            Assert.Equal("Employee", result.DataSet!.DataSetName);
            Assert.Equal(1, result.AffectedRows["Employee"]);

            Assert.NotNull(stub.LastSavedDataSet);
            var savedMaster = stub.LastSavedDataSet!.Tables["Employee"]!;
            Assert.Equal(DataRowState.Added, savedMaster.Rows[0].RowState);
        }
    }
}
