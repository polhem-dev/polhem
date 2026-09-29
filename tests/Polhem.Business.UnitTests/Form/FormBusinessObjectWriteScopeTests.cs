using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
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
    /// Record scope on the save path, against payloads a client can forge: row versions that disagree, detail rows
    /// that claim an owner they do not have, and new values placed outside the caller's scope.
    /// </summary>
    /// <remarks>
    /// The repository is a recording stub, so each test can assert which row id reached the authoritative checks as
    /// well as whether the save was refused. The scope filter has the shape the real resolver produces for the
    /// <c>Own</c> strategy: the owner column must be the caller.
    /// </remarks>
    public class FormBusinessObjectWriteScopeTests : IClassFixture<PolhemTestFixture>
    {
        private const string ProgId = "WsForm";
        private const string DetailTable = "WsForm_Item";
        private const string OwnerField = "owner_rowid";

        private static readonly Guid s_me = Guid.NewGuid();
        private static readonly Guid s_someoneElse = Guid.NewGuid();

        private readonly PolhemTestFixture _fx;

        public FormBusinessObjectWriteScopeTests(PolhemTestFixture fx) { _fx = fx; }

        // -------- Master rows --------

        [Fact]
        [DisplayName("Save refuses a Modified master whose Original rowid is someone else's record and Current rowid is in scope")]
        public void Save_MasterWithForgedOriginalRowId_ThrowsForbidden()
        {
            var victim = Guid.NewGuid();
            var mine = Guid.NewGuid();
            var repo = new RecordingRepo { InScope = { mine } };
            var ds = NewDataSet();
            var row = AddMaster(ds, victim, s_me);
            ds.AcceptChanges();
            row[SysFields.RowId] = mine;   // Current is in scope; the UPDATE would still be keyed on the victim.

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.DoesNotContain(mine, repo.ScopeChecks);
            Assert.Empty(repo.Saved);
        }

        [Fact]
        [DisplayName("Save checks record scope on the Original rowid of a Modified master, the key the UPDATE binds")]
        public void Save_ModifiedMaster_ChecksScopeOnOriginalRowId()
        {
            var rowId = Guid.NewGuid();
            var repo = new RecordingRepo { InScope = { rowId } };
            var ds = NewDataSet();
            var row = AddMaster(ds, rowId, s_me);
            ds.AcceptChanges();
            row["sys_id"] = "changed";

            Bo(repo).Save(new SaveArgs { DataSet = ds });

            Assert.Equal([rowId], repo.ScopeChecks);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("Save checks record scope on the Original rowid of a Deleted master and refuses it when out of scope")]
        public void Save_DeletedMasterOutOfScope_ThrowsForbidden()
        {
            var victim = Guid.NewGuid();
            var repo = new RecordingRepo();
            var ds = NewDataSet();
            var row = AddMaster(ds, victim, s_someoneElse);
            ds.AcceptChanges();
            row.Delete();

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.Equal([victim], repo.ScopeChecks);
        }

        // -------- Detail rows --------

        [Theory]
        [InlineData(DataRowState.Modified)]
        [InlineData(DataRowState.Deleted)]
        [DisplayName("Save refuses a detail row of someone else's record carried under the caller's own in-scope master")]
        public void Save_ForeignDetailUnderOwnMaster_ThrowsForbidden(DataRowState state)
        {
            var master = Guid.NewGuid();
            var victimMaster = Guid.NewGuid();
            var victimDetail = Guid.NewGuid();
            var repo = new RecordingRepo { InScope = { master }, StoredOwners = { [victimDetail] = victimMaster } };
            var ds = NewDataSet();
            AddMaster(ds, master, s_me);
            var detail = AddDetail(ds, victimDetail, master);   // Claims the caller's master in both versions.
            ds.AcceptChanges();
            Change(detail, state);

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.Equal([victimDetail], repo.StoredOwnerLookups);
            Assert.Empty(repo.Saved);
        }

        [Theory]
        [InlineData(DataRowState.Modified)]
        [InlineData(DataRowState.Deleted)]
        [DisplayName("Control case: a detail row the database already files under the caller's in-scope master is saved")]
        public void Save_OwnDetailUnderOwnMaster_Passes(DataRowState state)
        {
            var master = Guid.NewGuid();
            var detailRowId = Guid.NewGuid();
            var repo = new RecordingRepo { InScope = { master }, StoredOwners = { [detailRowId] = master } };
            var ds = NewDataSet();
            AddMaster(ds, master, s_me);
            var detail = AddDetail(ds, detailRowId, master);
            ds.AcceptChanges();
            Change(detail, state);

            var ex = Record.Exception(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Equal([detailRowId], repo.StoredOwnerLookups);
        }

        [Fact]
        [DisplayName("Save refuses a Modified detail row whose Original rowid differs from its Current rowid")]
        public void Save_DetailWithForgedOriginalRowId_ThrowsForbidden()
        {
            var master = Guid.NewGuid();
            var victimDetail = Guid.NewGuid();
            var repo = new RecordingRepo { InScope = { master } };
            var ds = NewDataSet();
            AddMaster(ds, master, s_me);
            var detail = AddDetail(ds, victimDetail, master);
            ds.AcceptChanges();
            detail[SysFields.RowId] = Guid.NewGuid();

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.Empty(repo.Saved);
        }

        // -------- New values --------

        [Fact]
        [DisplayName("Save refuses an Added master whose owner is outside the caller's Create scope")]
        public void Save_AddedMasterOwnedBySomeoneElse_ThrowsForbidden()
        {
            var repo = new RecordingRepo();
            var ds = NewDataSet();
            AddMaster(ds, Guid.NewGuid(), s_someoneElse);

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.Empty(repo.Saved);
        }

        [Fact]
        [DisplayName("Control case: an Added master owned by the caller is saved")]
        public void Save_AddedMasterOwnedByCaller_Passes()
        {
            var repo = new RecordingRepo();
            var ds = NewDataSet();
            AddMaster(ds, Guid.NewGuid(), s_me);

            var ex = Record.Exception(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("Save refuses a Modified master that moves an in-scope record to an owner outside the caller's Update scope")]
        public void Save_ModifiedMasterMovedToSomeoneElse_ThrowsForbidden()
        {
            var rowId = Guid.NewGuid();
            var repo = new RecordingRepo { InScope = { rowId } };
            var ds = NewDataSet();
            var row = AddMaster(ds, rowId, s_me);
            ds.AcceptChanges();
            row[OwnerField] = s_someoneElse;

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.Equal([rowId], repo.ScopeChecks);   // The stored row passed; the new value is what is refused.
            Assert.Empty(repo.Saved);
        }

        [Fact]
        [DisplayName("A form without a PermissionModelId saves an Added row with any owner, since it has no record scope")]
        public void Save_FormWithoutPermissionModel_IgnoresNewValueScope()
        {
            var repo = new RecordingRepo();
            var ds = NewDataSet();
            AddMaster(ds, Guid.NewGuid(), s_someoneElse);

            var ex = Record.Exception(() => Bo(repo, permissionModelId: string.Empty).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Single(repo.Saved);
        }

        // -------- Fixtures --------

        private FormBusinessObject Bo(RecordingRepo repo, string permissionModelId = "WsModel")
        {
            var defineAccess = new FormSchemaOverlayDefineAccess(
                _fx.GetRequiredService<IDefineAccess>(), BuildSchema(permissionModelId));
            var ctx = new BusinessObjectContext
            {
                DefineAccess = defineAccess,
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = new TestOverrideServiceProvider(_fx.Provider,
                [
                    (typeof(ICompanyAuthorizationService), new AllowAll()),
                    (typeof(IScopeResolver), new OwnScopeResolver()),
                    (typeof(IRepositoryFactory), new RepoFactory(repo)),
                ]),
            };
            return new FormBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx), ProgId);
        }

        private static FormSchema BuildSchema(string permissionModelId)
        {
            var schema = new FormSchema(ProgId, "Write scope") { CategoryId = "company", PermissionModelId = permissionModelId };
            var master = schema.Tables!.Add(ProgId, "Write scope");
            master.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            master.Fields.Add("sys_id", "ID", FieldDbType.String);
            master.Fields.Add(OwnerField, "Owner", FieldDbType.Guid).ScopeRole = ScopeRole.Owner;

            var detail = schema.Tables.Add(DetailTable, "Write scope item");
            detail.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            detail.Fields.Add(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid);
            detail.Fields.Add("qty", "Qty", FieldDbType.Integer);
            return schema;
        }

        private static DataSet NewDataSet()
        {
            var ds = new DataSet();
            var master = ds.Tables.Add(ProgId);
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add("sys_id");
            master.Columns.Add(OwnerField, typeof(Guid));

            var detail = ds.Tables.Add(DetailTable);
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Columns.Add("qty", typeof(int));
            return ds;
        }

        private static DataRow AddMaster(DataSet ds, Guid rowId, Guid owner)
        {
            var table = ds.Tables[ProgId]!;
            var row = table.NewRow();
            row[SysFields.RowId] = rowId;
            row["sys_id"] = "m";
            row[OwnerField] = owner;
            table.Rows.Add(row);
            return row;
        }

        private static DataRow AddDetail(DataSet ds, Guid rowId, Guid masterRowId)
        {
            var table = ds.Tables[DetailTable]!;
            var row = table.NewRow();
            row[SysFields.RowId] = rowId;
            row[SysFields.MasterRowId] = masterRowId;
            row["qty"] = 1;
            table.Rows.Add(row);
            return row;
        }

        private static void Change(DataRow row, DataRowState state)
        {
            if (state == DataRowState.Deleted) { row.Delete(); }
            else { row["qty"] = 2; }
        }

        private sealed class AllowAll : ICompanyAuthorizationService
        {
            public bool Can(Guid accessToken, string modelId, PermissionActions action) => true;
        }

        /// <summary>The <c>Own</c> strategy for every action: the owner column must be the caller.</summary>
        private sealed class OwnScopeResolver : IScopeResolver
        {
            public FilterNode? ResolveFilter(Guid accessToken, string modelId, PermissionActions action, FormSchema formSchema)
                => FilterCondition.In(OwnerField, [s_me]);
        }

        private sealed class RepoFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _repo;
            public RepoFactory(IDataFormRepository repo) { _repo = repo; }
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_repo;
            public T Create<T>(Guid accessToken = default) where T : class => throw new NotSupportedException();
        }

        private sealed class RecordingRepo : IDataFormRepository
        {
            /// <summary>The master rowids the authoritative scope check confirms.</summary>
            public HashSet<Guid> InScope { get; } = [];

            /// <summary>The stored owner of each detail row, keyed by the detail's rowid.</summary>
            public Dictionary<Guid, Guid> StoredOwners { get; } = [];

            public List<Guid> ScopeChecks { get; } = [];
            public List<Guid> StoredOwnerLookups { get; } = [];
            public List<DataSet> Saved { get; } = [];

            public bool ExistsInScope(Guid rowId, FilterNode? scopeFilter)
            {
                ScopeChecks.Add(rowId);
                return InScope.Contains(rowId);
            }

            public DataTable GetRowsByRowId(string tableName, string selectFields, IReadOnlyCollection<Guid> rowIds)
            {
                StoredOwnerLookups.AddRange(rowIds);
                var table = new DataTable(tableName);
                table.Columns.Add(SysFields.RowId, typeof(Guid));
                table.Columns.Add(SysFields.MasterRowId, typeof(Guid));
                foreach (var rowId in rowIds.Where(StoredOwners.ContainsKey))
                    table.Rows.Add(rowId, StoredOwners[rowId]);
                return table;
            }

            public (DataSet? Refreshed, Dictionary<string, int> AffectedRows) Save(DataSet dataSet)
            {
                Saved.Add(dataSet);
                return (null, []);
            }

            public DataFormListResult GetList(string selectFields, FilterNode? filter, SortFieldCollection? sortFields, PagingOptions? paging = null)
                => new() { Table = new DataTable() };
            public DataSet GetNewData(string timeZoneId = "") => new();
            public DataSet? GetData(Guid rowId, FilterNode? scopeFilter = null) => null;
            public int Delete(Guid rowId, FilterNode? scopeFilter = null) => 0;
        }
    }
}
