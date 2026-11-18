using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The rules of the user-query actions on <see cref="FormBusinessObject"/>, over an in-memory repository: who may
    /// see, change, delete and default which query. The SQL itself is covered by the repository's own tests.
    /// </summary>
    public class FormBusinessObjectUserQueryTests : IClassFixture<PolhemTestFixture>
    {
        private const string ProgId = "UqForm";
        private const string OtherProgId = "UqOther";
        private const string Me = "alice";
        private const string Other = "bob";

        private readonly PolhemTestFixture _fx;

        public FormBusinessObjectUserQueryTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("SaveUserQuery with an empty RowId inserts a query owned by the caller with a trimmed name")]
        public void SaveUserQuery_New_InsertsOwnedByCaller()
        {
            var repo = new MemoryRepo();

            var result = Bo(repo, Me).SaveUserQuery(new SaveUserQueryArgs { Name = "  Open orders ", IsShared = true, Criteria = "<x/>" });

            var stored = repo.Queries[result.RowId];
            Assert.Equal((ProgId, Me, "Open orders", true, "<x/>"), (stored.ProgId, stored.UserId, stored.Name, stored.IsShared, stored.Criteria));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("SaveUserQuery refuses a blank name")]
        public void SaveUserQuery_BlankName_Throws(string name)
        {
            var ex = Assert.Throws<UserMessageException>(() => Bo(new MemoryRepo(), Me).SaveUserQuery(new SaveUserQueryArgs { Name = name }));
            Assert.Equal(PolhemMessages.UserQueryNameRequired, ex.MessageKey);
        }

        [Fact]
        [DisplayName("SaveUserQuery refuses a name longer than the column and criteria longer than the limit")]
        public void SaveUserQuery_TooLong_Throws()
        {
            var bo = Bo(new MemoryRepo(), Me);

            var name = Assert.Throws<UserMessageException>(() => bo.SaveUserQuery(new SaveUserQueryArgs
            {
                Name = new string('n', FormBusinessObject.UserQueryNameMaxLength + 1),
            }));
            var criteria = Assert.Throws<UserMessageException>(() => bo.SaveUserQuery(new SaveUserQueryArgs
            {
                Name = "q",
                Criteria = new string('c', FormBusinessObject.UserQueryCriteriaMaxLength + 1),
            }));

            Assert.Equal(PolhemMessages.UserQueryNameTooLong, name.MessageKey);
            Assert.Equal(PolhemMessages.UserQueryTooLarge, criteria.MessageKey);
        }

        [Fact]
        [DisplayName("SaveUserQuery overwrites the caller's own query")]
        public void SaveUserQuery_Own_Overwrites()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Me, "Old", isShared: false);

            Bo(repo, Me).SaveUserQuery(new SaveUserQueryArgs { RowId = rowId, Name = "New", IsShared = true, Criteria = "c" });

            Assert.Equal(("New", true, "c"), (repo.Queries[rowId].Name, repo.Queries[rowId].IsShared, repo.Queries[rowId].Criteria));
        }

        [Fact]
        [DisplayName("SaveUserQuery refuses to overwrite a query another user shares, and leaves it as it was")]
        public void SaveUserQuery_OthersShared_IsForbidden()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Other, "Theirs", isShared: true);

            var ex = Assert.Throws<ForbiddenException>(() =>
                Bo(repo, Me).SaveUserQuery(new SaveUserQueryArgs { RowId = rowId, Name = "Mine now" }));

            Assert.Equal(PolhemMessages.UserQueryNotOwner, ex.MessageKey);
            Assert.Equal("Theirs", repo.Queries[rowId].Name);
        }

        [Theory]
        [InlineData(Other, false, ProgId)]
        [InlineData(Me, false, OtherProgId)]
        [DisplayName("SaveUserQuery reports another user's personal query, or one on another form, as missing")]
        public void SaveUserQuery_NotVisible_IsNotFound(string owner, bool isShared, string progId)
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(progId, owner, "Hidden", isShared);

            var ex = Assert.Throws<UserMessageException>(() =>
                Bo(repo, Me).SaveUserQuery(new SaveUserQueryArgs { RowId = rowId, Name = "x" }));

            Assert.Equal(PolhemMessages.UserQueryNotFound, ex.MessageKey);
        }

        [Fact]
        [DisplayName("DeleteUserQuery deletes the caller's own query")]
        public void DeleteUserQuery_Own_Deletes()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Me, "Mine", isShared: false);

            var result = Bo(repo, Me).DeleteUserQuery(new DeleteUserQueryArgs { RowId = rowId });

            Assert.Equal(1, result.RowsAffected);
            Assert.False(repo.Queries.ContainsKey(rowId));
        }

        [Fact]
        [DisplayName("DeleteUserQuery refuses a query another user shares")]
        public void DeleteUserQuery_OthersShared_IsForbidden()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Other, "Theirs", isShared: true);

            Assert.Throws<ForbiddenException>(() => Bo(repo, Me).DeleteUserQuery(new DeleteUserQueryArgs { RowId = rowId }));
            Assert.True(repo.Queries.ContainsKey(rowId));
        }

        [Fact]
        [DisplayName("DeleteUserQuery of a query the caller cannot see deletes nothing and reports zero")]
        public void DeleteUserQuery_NotVisible_ReportsZero()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Other, "Personal", isShared: false);

            var result = Bo(repo, Me).DeleteUserQuery(new DeleteUserQueryArgs { RowId = rowId });

            Assert.Equal(0, result.RowsAffected);
            Assert.True(repo.Queries.ContainsKey(rowId));
        }

        [Fact]
        [DisplayName("SetDefaultUserQuery accepts a query another user shares and records it for the caller only")]
        public void SetDefaultUserQuery_OthersShared_IsAccepted()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Other, "Theirs", isShared: true);

            var result = Bo(repo, Me).SetDefaultUserQuery(new SetDefaultUserQueryArgs { RowId = rowId });

            Assert.Equal(rowId, result.DefaultRowId);
            Assert.Equal(rowId, repo.Defaults[(ProgId, Me)]);
            Assert.False(repo.Defaults.ContainsKey((ProgId, Other)));
        }

        [Fact]
        [DisplayName("SetDefaultUserQuery refuses another user's personal query as missing")]
        public void SetDefaultUserQuery_OthersPersonal_IsNotFound()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Other, "Personal", isShared: false);

            Assert.Throws<UserMessageException>(() => Bo(repo, Me).SetDefaultUserQuery(new SetDefaultUserQueryArgs { RowId = rowId }));
            Assert.Empty(repo.Defaults);
        }

        [Fact]
        [DisplayName("SetDefaultUserQuery with an empty RowId clears the caller's default")]
        public void SetDefaultUserQuery_Empty_Clears()
        {
            var repo = new MemoryRepo();
            repo.Defaults[(ProgId, Me)] = repo.Add(ProgId, Me, "Mine", isShared: false);

            Bo(repo, Me).SetDefaultUserQuery(new SetDefaultUserQueryArgs { RowId = Guid.Empty });

            Assert.Empty(repo.Defaults);
        }

        [Fact]
        [DisplayName("GetUserQueries reports a default the caller can no longer apply as none")]
        public void GetUserQueries_UnsharedDefault_ReportsNone()
        {
            var repo = new MemoryRepo();
            var mine = repo.Add(ProgId, Me, "Mine", isShared: false);
            var unshared = repo.Add(ProgId, Other, "Was shared", isShared: false);
            repo.Defaults[(ProgId, Me)] = unshared;

            var hidden = Bo(repo, Me).GetUserQueries(new GetUserQueriesArgs());
            repo.Defaults[(ProgId, Me)] = mine;
            var shown = Bo(repo, Me).GetUserQueries(new GetUserQueriesArgs());

            Assert.Equal(Guid.Empty, hidden.DefaultRowId);
            Assert.Equal(mine, shown.DefaultRowId);
            Assert.Equal(mine, (Guid)Assert.Single(shown.Table!.Rows.Cast<DataRow>())["sys_rowid"]);
        }

        [Fact]
        [DisplayName("Every user-query action requires the form's Read permission")]
        public void UserQueryActions_WithoutRead_AreForbidden()
        {
            var repo = new MemoryRepo();
            var rowId = repo.Add(ProgId, Me, "Mine", isShared: false);
            var bo = Bo(repo, Me, canRead: false);

            Assert.Throws<ForbiddenException>(() => bo.GetUserQueries(new GetUserQueriesArgs()));
            Assert.Throws<ForbiddenException>(() => bo.SaveUserQuery(new SaveUserQueryArgs { Name = "q" }));
            Assert.Throws<ForbiddenException>(() => bo.DeleteUserQuery(new DeleteUserQueryArgs { RowId = rowId }));
            Assert.Throws<ForbiddenException>(() => bo.SetDefaultUserQuery(new SetDefaultUserQueryArgs { RowId = rowId }));
            Assert.True(repo.Queries.ContainsKey(rowId));
            Assert.Single(repo.Queries);
        }

        private FormBusinessObject Bo(MemoryRepo repo, string userId, bool canRead = true)
        {
            var schema = new FormSchema(ProgId, "User query form") { CategoryId = "company", PermissionModelId = "UqModel" };
            var ctx = new BusinessObjectContext
            {
                DefineAccess = new FormSchemaOverlayDefineAccess(_fx.GetRequiredService<IDefineAccess>(), schema),
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = new TestOverrideServiceProvider(_fx.Provider,
                [
                    (typeof(ICompanyAuthorizationService), new ReadOnlyGrant(canRead)),
                    (typeof(IRepositoryFactory), new RepoFactory(repo)),
                ]),
            };
            return new FormBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx, userId, companyId: "c001"), ProgId);
        }

        private sealed class ReadOnlyGrant(bool canRead) : ICompanyAuthorizationService
        {
            public bool Can(SessionCompanyScope companyScope, string modelId, PermissionActions action) => Can(Guid.Empty, modelId, action);

            public bool Can(Guid accessToken, string modelId, PermissionActions action) => canRead && action == PermissionActions.Read;
        }

        private sealed class RepoFactory(MemoryRepo repo) : IRepositoryFactory
        {
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => throw new NotSupportedException();

            public T CreateFormRepository<T>(Guid accessToken, string progId, SessionCompanyScope companyScope)
                where T : class, IDataFormRepository
                => throw new NotSupportedException();

            public T Create<T>(Guid accessToken = default) where T : class => (T)(object)repo;
        }

        private sealed record StoredQuery(string ProgId, string UserId, string Name, bool IsShared, string Criteria);

        private sealed class MemoryRepo : IUserQueryRepository
        {
            public Dictionary<Guid, StoredQuery> Queries { get; } = [];

            public Dictionary<(string ProgId, string UserId), Guid> Defaults { get; } = [];

            public Guid Add(string progId, string userId, string name, bool isShared)
            {
                var rowId = Guid.NewGuid();
                Queries[rowId] = new StoredQuery(progId, userId, name, isShared, string.Empty);
                return rowId;
            }

            public UserQueryList GetQueries(string progId, string userId)
            {
                var table = new DataTable();
                table.Columns.Add("sys_rowid", typeof(Guid));
                foreach (var (rowId, query) in Queries)
                {
                    if (query.ProgId == progId && (query.UserId == userId || query.IsShared))
                        table.Rows.Add(rowId);
                }
                return new UserQueryList(table, Defaults.GetValueOrDefault((progId, userId)));
            }

            public UserQueryOwner? GetOwner(Guid rowId)
                => Queries.TryGetValue(rowId, out var q) ? new UserQueryOwner(q.ProgId, q.UserId, q.IsShared) : null;

            public void Insert(Guid rowId, string progId, string userId, string name, bool isShared, string criteria)
                => Queries[rowId] = new StoredQuery(progId, userId, name, isShared, criteria);

            public int Update(Guid rowId, string name, bool isShared, string criteria)
            {
                Queries[rowId] = Queries[rowId] with { Name = name, IsShared = isShared, Criteria = criteria };
                return 1;
            }

            public int Delete(Guid rowId)
            {
                foreach (var key in Defaults.Where(d => d.Value == rowId).Select(d => d.Key).ToList())
                    Defaults.Remove(key);
                return Queries.Remove(rowId) ? 1 : 0;
            }

            public void SetDefault(string progId, string userId, Guid rowId)
            {
                if (rowId == Guid.Empty)
                    Defaults.Remove((progId, userId));
                else
                    Defaults[(progId, userId)] = rowId;
            }
        }
    }
}
