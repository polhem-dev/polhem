using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Server.UnitTests.Dispatch;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;

namespace Polhem.Api.Server.UnitTests.Form
{
    /// <summary>
    /// The user-query actions of <c>Employee</c> through the JSON-RPC dispatcher, over a stub repository: each action
    /// resolves to its <c>FormBusinessObject</c> method, the request converts to its arguments and the result to its
    /// response. The rules are covered by <c>FormBusinessObjectUserQueryTests</c>, the SQL by
    /// <c>UserQueryRepositoryTests</c>.
    /// </summary>
    public class UserQueryJsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
    {
        private const string UserId = "uq-dispatch";

        private readonly PolhemTestFixture _fx;

        public UserQueryJsonRpcRoundTripTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("Employee.SaveUserQuery, SetDefaultUserQuery, GetUserQueries and DeleteUserQuery dispatch and return their responses")]
        public async Task UserQueryActions_ThroughJsonRpc_RoundTrip()
        {
            var repo = new StubRepo();
            var dispatcher = CreateDispatcher(repo);

            var saved = Assert.IsType<SaveUserQueryResponse>((await Dispatch(dispatcher, FormActions.SaveUserQuery,
                new SaveUserQueryRequest { Name = "Mine", IsShared = true, Criteria = "<UserQuery version=\"1\" />" })).Result!.Value);
            var defaulted = Assert.IsType<SetDefaultUserQueryResponse>((await Dispatch(dispatcher, FormActions.SetDefaultUserQuery,
                new SetDefaultUserQueryRequest { RowId = saved.RowId })).Result!.Value);
            var listed = Assert.IsType<GetUserQueriesResponse>((await Dispatch(dispatcher, FormActions.GetUserQueries,
                new GetUserQueriesRequest())).Result!.Value);
            var deleted = Assert.IsType<DeleteUserQueryResponse>((await Dispatch(dispatcher, FormActions.DeleteUserQuery,
                new DeleteUserQueryRequest { RowId = saved.RowId })).Result!.Value);

            Assert.NotEqual(Guid.Empty, saved.RowId);
            Assert.Equal(("Employee", UserId, "Mine", true, "<UserQuery version=\"1\" />"), repo.Inserted);
            Assert.Equal(saved.RowId, defaulted.DefaultRowId);
            Assert.Equal(saved.RowId, listed.DefaultRowId);
            Assert.Equal(saved.RowId, (Guid)Assert.Single(listed.Table!.Rows.Cast<DataRow>())["sys_rowid"]);
            Assert.Equal(1, deleted.RowsAffected);
        }

        private TestDispatcher CreateDispatcher(StubRepo repo)
        {
            var overrideServices = new TestOverrideServiceProvider(_fx.Provider, (typeof(IRepositoryFactory), new StubFactory(repo)));
            var boFactory = new BusinessObjectFactory(
                overrideServices,
                _fx.GetRequiredService<IDefineAccess>(),
                _fx.GetRequiredService<ISessionInfoService>(),
                _fx.GetRequiredService<ILanguageService>(),
                _fx.GetRequiredService<IBoTypeResolver>());
            return new TestDispatcher(_fx.Provider, boFactory)
            {
                AccessToken = TestSessionFactory.CreateAccessToken(_fx, UserId),
                IsLocalCall = true,
            };
        }

        private static async Task<TestRpcResponse> Dispatch(TestDispatcher dispatcher, string action, object value)
        {
            var response = await dispatcher.ExecuteAsync(new TestRpcRequest
            {
                Method = $"Employee.{action}",
                Params = new TestPayload { Value = value },
                Id = Guid.NewGuid().ToString(),
            });
            Assert.Null(response.Error);
            return response;
        }

        private sealed class StubFactory(StubRepo repo) : IRepositoryFactory
        {
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => throw new NotSupportedException();

            public T CreateFormRepository<T>(Guid accessToken, string progId, SessionCompanyScope companyScope)
                where T : class, IDataFormRepository
                => throw new NotSupportedException();

            public T Create<T>(Guid accessToken = default) where T : class => (T)(object)repo;
        }

        private sealed class StubRepo : IUserQueryRepository
        {
            private Guid _rowId;
            private Guid _default;

            public (string ProgId, string UserId, string Name, bool IsShared, string Criteria) Inserted { get; private set; }

            public UserQueryList GetQueries(string progId, string userId)
            {
                var table = new DataTable("UserQuery");
                table.Columns.Add("sys_rowid", typeof(Guid));
                table.Rows.Add(_rowId);
                return new UserQueryList(table, _default);
            }

            public UserQueryOwner? GetOwner(Guid rowId)
                => rowId == _rowId ? new UserQueryOwner(Inserted.ProgId, Inserted.UserId, Inserted.IsShared) : null;

            public void Insert(Guid rowId, string progId, string userId, string name, bool isShared, string criteria)
            {
                _rowId = rowId;
                Inserted = (progId, userId, name, isShared, criteria);
            }

            public int Update(Guid rowId, string name, bool isShared, string criteria) => 1;

            public int Delete(Guid rowId) => rowId == _rowId ? 1 : 0;

            public void SetDefault(string progId, string userId, Guid rowId) => _default = rowId;
        }
    }
}
