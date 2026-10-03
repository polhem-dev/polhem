using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.Messages.Form;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;
using Polhem.Api.Core.UnitTests.Dispatch;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// An end-to-end round-trip through the JSON-RPC dispatcher: <c>Employee.GetList</c> is dispatched by the
    /// dispatcher to <c>FormBusinessObject.GetList</c>, and a stub <c>IDataFormRepository</c> returns a known
    /// DataTable. It verifies that:
    /// <list type="bullet">
    /// <item>action routing (the reflection lookup of progId.action) finds the method</item>
    /// <item>ApiInputConverter (GetListRequest → GetListArgs) keeps Filter / SortFields</item>
    /// <item>ApiOutputConverter (GetListResult → GetListResponse) name-convention reflection works</item>
    /// </list>
    /// No real database is involved; the SQL behavior on the database side is covered by
    /// <c>FormBusinessObjectGetListTests</c>.
    /// </summary>
    public class GetListJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public GetListJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.GetList through the JSON-RPC dispatcher dispatches to FormBusinessObject.GetList and returns the stub DataTable")]
        public async Task GetList_ThroughJsonRpc_DispatchesAndReturnsTable()
        {
            // Arrange
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Rows.Add("E001", "員工甲");
            table.Rows.Add("E002", "員工乙");

            var stubRepository = new StubDataFormRepository(table);
            var stubFactory = new StubFormRepositoryFactory(stubRepository);

            var overrideServices = new TestOverrideServiceProvider(
                _fx.Provider,
                (typeof(IRepositoryFactory), stubFactory));

            // A hand-built BO factory gets the overriding `IServiceProvider`, so when the production BO resolves
            // `IRepositoryFactory` through `BusinessObjectContext.Services` it receives the stub.
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

            var rowId = Guid.NewGuid();
            var request = new TestRpcRequest
            {
                Method = $"Employee.{FormActions.GetList}",
                Params = new TestPayload
                {
                    Value = new GetListRequest
                    {
                        SelectFields = "sys_id,sys_name",
                        Filter = FilterCondition.Equal("sys_rowid", rowId),
                        SortFields = [new SortField("sys_id", SortDirection.Asc)],
                    },
                },
                Id = Guid.NewGuid().ToString(),
            };

            // Act
            var response = await executor.ExecuteAsync(request);

            // Assert
            Assert.Null(response.Error);
            var result = Assert.IsType<GetListResponse>(response.Result!.Value);
            Assert.NotNull(result.Table);
            Assert.Equal(2, result.Table!.Rows.Count);
            Assert.Equal("E001", result.Table.Rows[0]["sys_id"]);
            Assert.Equal("員工乙", result.Table.Rows[1]["sys_name"]);

            // Assert: the args the stub received show that ApiInputConverter kept Filter / SortFields.
            Assert.Equal("sys_id,sys_name", stubRepository.LastSelectFields);
            var condition = Assert.IsType<FilterCondition>(stubRepository.LastFilter);
            Assert.Equal("sys_rowid", condition.FieldName);
            // A Plain body travels as JSON, so the value arrives as text; the repository converts it by the field's
            // type (GetListFilterValueDbTests runs that against each database).
            Assert.Equal(rowId.ToString(), condition.Value?.ToString());
            Assert.NotNull(stubRepository.LastSortFields);
            Assert.Single(stubRepository.LastSortFields!);
            Assert.Equal("sys_id", stubRepository.LastSortFields![0].FieldName);
            // A request without paging reaches the repository as the first page of the framework cap.
            Assert.NotNull(stubRepository.LastPaging);
            Assert.Equal(1, stubRepository.LastPaging!.Page);
            Assert.Equal(PagingOptions.MaxPageSize, stubRepository.LastPaging.PageSize);
        }

        [Fact]
        [DisplayName("Employee.GetList with Paging is passed through to the BO by the executor, and the returned PagingInfo is mapped to Response.Paging by ApiOutputConverter")]
        public async Task GetList_ThroughJsonRpc_PreservesPagingAndReturnsPagingInfo()
        {
            // Arrange
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Rows.Add("E001");
            table.Rows.Add("E002");

            var stubPagingInfo = new PagingInfo
            {
                Page = 2,
                PageSize = 25,
                TotalCount = 100,
                HasMore = true,
            };
            var stubRepository = new StubDataFormRepository(table, stubPagingInfo);
            var stubFactory = new StubFormRepositoryFactory(stubRepository);

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
                Method = $"Employee.{FormActions.GetList}",
                Params = new TestPayload
                {
                    Value = new GetListRequest
                    {
                        Paging = new PagingOptions { Page = 2, PageSize = 25, IncludeTotalCount = true },
                    },
                },
                Id = Guid.NewGuid().ToString(),
            };

            // Act
            var response = await executor.ExecuteAsync(request);

            // Assert: the PagingOptions the stub received.
            Assert.Null(response.Error);
            Assert.NotNull(stubRepository.LastPaging);
            Assert.Equal(2, stubRepository.LastPaging!.Page);
            Assert.Equal(25, stubRepository.LastPaging.PageSize);
            Assert.True(stubRepository.LastPaging.IncludeTotalCount);

            // Assert: `response.Paging` is copied by the ApiOutputConverter name convention.
            var result = Assert.IsType<GetListResponse>(response.Result!.Value);
            Assert.NotNull(result.Paging);
            Assert.Equal(2, result.Paging!.Page);
            Assert.Equal(25, result.Paging.PageSize);
            Assert.Equal(100, result.Paging.TotalCount);
            Assert.True(result.Paging.HasMore);
        }

        internal sealed class StubFormRepositoryFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _data;
            public StubFormRepositoryFactory(IDataFormRepository data) { _data = data; }
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_data;
            public T Create<T>(Guid accessToken = default) where T : class
                => throw new NotSupportedException();
        }

        internal sealed class StubDataFormRepository : IDataFormRepository
        {
            private readonly DataTable _table;
            private readonly PagingInfo? _paging;

            public StubDataFormRepository(DataTable table, PagingInfo? paging = null)
            {
                _table = table;
                _paging = paging;
            }

            public string? LastSelectFields { get; private set; }
            public FilterNode? LastFilter { get; private set; }
            public SortFieldCollection? LastSortFields { get; private set; }
            public PagingOptions? LastPaging { get; private set; }

            public DataFormListResult GetList(
                string selectFields,
                FilterNode? filter,
                SortFieldCollection? sortFields,
                PagingOptions? paging = null)
            {
                LastSelectFields = selectFields;
                LastFilter = filter;
                LastSortFields = sortFields;
                LastPaging = paging;
                return new DataFormListResult { Table = _table, Paging = _paging };
            }

            public DataSet GetNewData(string timeZoneId = "") => throw new NotSupportedException();

            public DataSet? GetData(Guid rowId, FilterNode? scopeFilter = null) => throw new NotSupportedException();

            public DataTable GetRowsByRowId(string tableName, string selectFields, IReadOnlyCollection<Guid> rowIds)
                => throw new NotSupportedException();

            public (DataSet? Refreshed, Dictionary<string, int> AffectedRows) Save(DataSet dataSet)
                => throw new NotSupportedException();

            public int Delete(Guid rowId, FilterNode? scopeFilter = null) => throw new NotSupportedException();

            public bool ExistsInScope(Guid rowId, FilterNode? scopeFilter) => true;
        }
    }
}
