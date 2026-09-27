using System.ComponentModel;
using System.Data;
using Polhem.Base.Data;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Paging;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <see cref="FormBusinessObject.GetLookup"/> skips the <c>Read</c> permission gate by design, but still limits its
    /// candidates to the caller's <c>Read</c> record scope unless the business object opts out.
    /// </summary>
    /// <remarks>
    /// Before the change, any authenticated user could page through the id and name of every row of any form with a
    /// permission model, including the rows of other departments.
    /// </remarks>
    public class FormBusinessObjectLookupScopeTests : IClassFixture<PolhemTestFixture>
    {
        private const string ProgId = "LkScopeForm";

        /// <summary>The filter the stub resolver hands out for the Read scope, recognizable by reference.</summary>
        private static readonly FilterNode s_readScope = FilterCondition.In("owner_rowid", [Guid.NewGuid()]);

        private readonly PolhemTestFixture _fx;

        public FormBusinessObjectLookupScopeTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("GetLookup AND-combines the caller's Read record scope into the candidate query")]
        public void GetLookup_ScopedForm_AppliesReadScope()
        {
            var repo = new CapturingRepo();

            new FormBusinessObject(Context(repo, "LkModel"), Token(), ProgId).GetLookup(new GetLookupArgs { SearchText = "a" });

            Assert.True(ContainsNode(repo.LastFilter, s_readScope));
        }

        [Fact]
        [DisplayName("A business object that overrides LookupAppliesRecordScope to false gets every candidate row")]
        public void GetLookup_OptedOut_OmitsReadScope()
        {
            var repo = new CapturingRepo();

            new SharedMasterBo(Context(repo, "LkModel"), Token(), ProgId).GetLookup(new GetLookupArgs { SearchText = "a" });

            Assert.False(ContainsNode(repo.LastFilter, s_readScope));
            Assert.NotNull(repo.LastFilter);   // The search filter is still there.
        }

        [Fact]
        [DisplayName("GetLookup on a form without a permission model applies no record scope")]
        public void GetLookup_FormWithoutPermissionModel_AppliesNoScope()
        {
            var repo = new CapturingRepo();

            new FormBusinessObject(Context(repo, string.Empty), Token(), ProgId).GetLookup(new GetLookupArgs());

            Assert.Null(repo.LastFilter);
        }

        private Guid Token() => TestSessionFactory.CreateAccessToken(_fx);

        private BusinessObjectContext Context(CapturingRepo repo, string permissionModelId)
        {
            var schema = new FormSchema(ProgId, "Lookup scope") { CategoryId = "company", PermissionModelId = permissionModelId };
            var table = schema.Tables!.Add(ProgId, "Lookup scope");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "ID", FieldDbType.String) { MaxLength = 50 });
            table.Fields!.Add(new FormField("sys_name", "Name", FieldDbType.String) { MaxLength = 50 });

            return new BusinessObjectContext
            {
                DefineAccess = new FormSchemaOverlayDefineAccess(_fx.GetRequiredService<IDefineAccess>(), schema),
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = new TestOverrideServiceProvider(_fx.Provider,
                [
                    (typeof(IScopeResolver), new ReadScopeResolver()),
                    (typeof(IRepositoryFactory), new RepoFactory(repo)),
                ]),
            };
        }

        private static bool ContainsNode(FilterNode? node, FilterNode target)
            => ReferenceEquals(node, target)
               || (node is FilterGroup group && group.Nodes.Any(child => ContainsNode(child, target)));

        /// <summary>A shared master every user may pick from, whatever their record scope.</summary>
        private sealed class SharedMasterBo : FormBusinessObject
        {
            public SharedMasterBo(IBusinessObjectContext ctx, Guid accessToken, string progId)
                : base(ctx, accessToken, progId) { }

            protected override bool LookupAppliesRecordScope => false;
        }

        private sealed class ReadScopeResolver : IScopeResolver
        {
            public FilterNode? ResolveFilter(Guid accessToken, string modelId, PermissionActions action, FormSchema formSchema)
                => action == PermissionActions.Read ? s_readScope : null;
        }

        private sealed class RepoFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _repo;
            public RepoFactory(IDataFormRepository repo) { _repo = repo; }
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_repo;
            public T Create<T>(Guid accessToken = default) where T : class => throw new NotSupportedException();
        }

        private sealed class CapturingRepo : IDataFormRepository
        {
            public FilterNode? LastFilter { get; private set; }

            public DataFormListResult GetList(string selectFields, FilterNode? filter, SortFieldCollection? sortFields, PagingOptions? paging = null)
            {
                LastFilter = filter;
                return new DataFormListResult { Table = new DataTable() };
            }

            public DataSet GetNewData(string timeZoneId = "") => new();
            public DataSet? GetData(Guid rowId, FilterNode? scopeFilter = null) => null;
            public DataTable GetRowsByRowId(string tableName, string selectFields, IReadOnlyCollection<Guid> rowIds) => new();
            public (DataSet? Refreshed, Dictionary<string, int> AffectedRows) Save(DataSet dataSet) => (null, []);
            public int Delete(Guid rowId, FilterNode? scopeFilter = null) => 0;
            public bool ExistsInScope(Guid rowId, FilterNode? scopeFilter) => false;
        }
    }
}
