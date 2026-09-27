using System.ComponentModel;
using System.Data;
using Polhem.Base.Exceptions;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Identity;
using Polhem.Definition.Paging;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// Tests of the layer-one permission gate in FormBusinessObject. A fake ICompanyAuthorizationService controls the result of Can,
    /// verifying that an unauthorized action is blocked (ForbiddenException), an authorized one passes, Save checks each row's RowState, and an empty
    /// PermissionModelId skips the gate. The interception happens before the repository, so no real DB is needed.
    /// </summary>
    /// <remarks>
    /// NOTE: "no real DB is needed" used to be only an intention. The BO was constructed with a bare <c>Guid.NewGuid()</c>, and inside the methods
    /// <c>SessionInfoService.Get</c> (for the current company and the locale) found nothing and took the rebuild path that reads
    /// <c>st_session</c>, so the whole class actually needed the container. It only became true after switching to
    /// <see cref="TestSessionFactory.CreateAccessToken"/>.
    /// </summary>
    public class FormBusinessObjectPermissionGateTests : IClassFixture<PolhemTestFixture>
    {
        // FormSchema 'PermGateForm' declares PermissionModelId='PermGateModel', so the gate is enabled.
        private const string GatedProgId = "PermGateForm";
        // 'Employee' declares no PermissionModelId, so the gate is skipped.
        private const string UngatedProgId = "Employee";

        private readonly PolhemTestFixture _fx;
        public FormBusinessObjectPermissionGateTests(PolhemTestFixture fx) { _fx = fx; }

        private FormBusinessObject Bo(PermissionActions allowed, IDataFormRepository? repo = null, string progId = GatedProgId)
        {
            var overrides = new List<(Type, object?)>
            {
                (typeof(ICompanyAuthorizationService), new FakeAuth(allowed)),
                (typeof(IScopeResolver), new StubScopeResolver()),
            };
            if (repo != null) { overrides.Add((typeof(IRepositoryFactory), new FakeFactory(repo))); }
            var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, overrides.ToArray());
            return new FormBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx), progId);
        }

        private static DataSet AddedRowDataSet()
        {
            var ds = new DataSet();
            var table = ds.Tables.Add(GatedProgId);
            table.Columns.Add("sys_id");
            var row = table.NewRow();
            row["sys_id"] = "x";
            table.Rows.Add(row); // RowState = Added
            return ds;
        }

        private static DataSet ModifiedRowDataSet()
        {
            var ds = new DataSet();
            var table = ds.Tables.Add(GatedProgId);
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_id");
            var row = table.NewRow();
            row["sys_rowid"] = Guid.NewGuid();
            row["sys_id"] = "x";
            table.Rows.Add(row);
            table.AcceptChanges();   // → Unchanged
            row["sys_id"] = "y";     // → Modified (triggers the layer-two Update check)
            return ds;
        }

        // The master is Unchanged and only the detail rows are edited. This is still an edit-and-save of an existing record, so it counts as Update.
        private static DataSet DetailOnlyEditDataSet()
        {
            var ds = new DataSet();
            var master = ds.Tables.Add(GatedProgId);
            master.Columns.Add("sys_rowid", typeof(Guid));
            master.Columns.Add("sys_id");
            var mrow = master.NewRow();
            mrow["sys_rowid"] = Guid.NewGuid();
            mrow["sys_id"] = "m";
            master.Rows.Add(mrow);

            var detail = ds.Tables.Add(GatedProgId + "_Item");
            detail.Columns.Add("sys_rowid", typeof(Guid));
            detail.Columns.Add("qty");
            var drow = detail.NewRow();
            drow["sys_rowid"] = Guid.NewGuid();
            drow["qty"] = "1";
            detail.Rows.Add(drow);

            ds.AcceptChanges();   // Everything Unchanged.
            drow["qty"] = "2";    // Detail → Modified, master stays Unchanged.
            return ds;
        }

        /// <summary>
        /// The master table is omitted entirely and only detail rows are sent. Before the fix, the layer-two check returned early,
        /// and the repository wrote the detail rows anyway.
        /// </summary>
        private static DataSet DetailRowsWithoutMasterTableDataSet()
        {
            var ds = new DataSet();
            var detail = ds.Tables.Add(GatedProgId + "_Item");
            detail.Columns.Add("sys_rowid", typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Columns.Add("qty");
            var drow = detail.NewRow();
            drow["sys_rowid"] = Guid.NewGuid();
            drow[SysFields.MasterRowId] = Guid.NewGuid();   // Someone else's master.
            drow["qty"] = "1";
            detail.Rows.Add(drow);
            ds.AcceptChanges();
            drow["qty"] = "2";
            return ds;
        }

        /// <summary>
        /// Carries one master, with a detail row in state <paramref name="state"/> pointing to another master that is <b>not in the payload</b>.
        /// </summary>
        /// <param name="state">The state the detail row should be in.</param>
        private static DataSet MasterWithDetailOwnedByAbsentMaster(DataRowState state)
        {
            var (ds, _, drow) = BuildMasterDetail();
            drow[SysFields.MasterRowId] = Guid.NewGuid();   // Someone else's master.

            if (state == DataRowState.Added) { return ds; }

            ds.AcceptChanges();
            drow["qty"] = "2";                             // → Modified, and both versions of MasterRowId belong to someone else.
            return ds;
        }

        /// <summary>
        /// A detail row that belonged to someone else's master is reparented to the master carried in this payload.
        /// </summary>
        /// <remarks>
        /// Current points to the master that is present; only Original points to the absent one. <b>An implementation that checks only Current
        /// misses this case</b>, yet it also moves data out of someone else's record. The only reason this case exists is to
        /// pin down that <c>WrittenVersions</c> returns both versions for Modified.
        /// </remarks>
        private static DataSet MasterWithDetailReparentedFromAbsentMaster()
        {
            var (ds, masterRowId, drow) = BuildMasterDetail();
            drow[SysFields.MasterRowId] = Guid.NewGuid();   // Someone else's master.
            ds.AcceptChanges();
            drow[SysFields.MasterRowId] = masterRowId;      // → Modified, reparented to the master that is present.
            return ds;
        }

        /// <summary>
        /// Builds one master row plus one detail row, and returns the DataSet, the master rowid and the detail row (all in the Added state).
        /// </summary>
        private static (DataSet DataSet, Guid MasterRowId, DataRow DetailRow) BuildMasterDetail()
        {
            var ds = new DataSet();
            var masterRowId = Guid.NewGuid();

            var master = ds.Tables.Add(GatedProgId);
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add("sys_id");
            var mrow = master.NewRow();
            mrow[SysFields.RowId] = masterRowId;
            mrow["sys_id"] = "m";
            master.Rows.Add(mrow);

            var detail = ds.Tables.Add(GatedProgId + "_Item");
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Columns.Add("qty");
            var drow = detail.NewRow();
            drow[SysFields.RowId] = Guid.NewGuid();
            drow["qty"] = "1";
            detail.Rows.Add(drow);

            return (ds, masterRowId, drow);
        }

        /// <summary>
        /// A well-formed detail-only edit: the master is present (Unchanged) and the detail row points to it.
        /// </summary>
        private static DataSet WellFormedDetailEditDataSet()
        {
            var ds = new DataSet();
            var masterRowId = Guid.NewGuid();

            var master = ds.Tables.Add(GatedProgId);
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add("sys_id");
            var mrow = master.NewRow();
            mrow[SysFields.RowId] = masterRowId;
            mrow["sys_id"] = "m";
            master.Rows.Add(mrow);

            var detail = ds.Tables.Add(GatedProgId + "_Item");
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Columns.Add("qty");
            var drow = detail.NewRow();
            drow[SysFields.RowId] = Guid.NewGuid();
            drow[SysFields.MasterRowId] = masterRowId;
            drow["qty"] = "1";
            detail.Rows.Add(drow);

            ds.AcceptChanges();
            drow["qty"] = "2";
            return ds;
        }

        [Fact]
        [DisplayName("GetList without the Read grant throws ForbiddenException")]
        public void GetList_NoReadGrant_ThrowsForbidden()
            => Assert.Throws<ForbiddenException>(() => Bo(PermissionActions.None).GetList(new GetListArgs()));

        [Fact]
        [DisplayName("GetData without the Read grant throws ForbiddenException")]
        public void GetData_NoReadGrant_ThrowsForbidden()
            => Assert.Throws<ForbiddenException>(() => Bo(PermissionActions.None).GetData(new GetDataArgs { RowId = Guid.NewGuid() }));

        [Fact]
        [DisplayName("Delete without the Delete grant throws ForbiddenException")]
        public void Delete_NoDeleteGrant_ThrowsForbidden()
            => Assert.Throws<ForbiddenException>(() => Bo(PermissionActions.None).Delete(new DeleteArgs { RowId = Guid.NewGuid() }));

        [Fact]
        [DisplayName("Save with an Added row is blocked without the Create grant (each row's RowState maps to Create)")]
        public void Save_AddedRow_NoCreateGrant_ThrowsForbidden()
        {
            // Holding Update|Delete but not Create, so the Create required by the Added row is blocked.
            var bo = Bo(PermissionActions.Update | PermissionActions.Delete);
            Assert.Throws<ForbiddenException>(() => bo.Save(new SaveArgs { DataSet = AddedRowDataSet() }));
        }

        [Fact]
        [DisplayName("GetList with the Read grant passes through to the repository")]
        public void GetList_WithReadGrant_PassesGate()
        {
            var bo = Bo(PermissionActions.Read, new StubRepo());

            var ex = Record.Exception(() => bo.GetList(new GetListArgs()));

            Assert.Null(ex); // The gate passes and the repository returns the stub.
        }

        [Fact]
        [DisplayName("Save with an Added row passes with the Create grant")]
        public void Save_AddedRow_WithCreateGrant_PassesGate()
        {
            var bo = Bo(PermissionActions.Create, new StubRepo());

            var ex = Record.Exception(() => bo.Save(new SaveArgs { DataSet = AddedRowDataSet() }));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("Save with a Modified row whose record is out of scope (ExistsInScope=false) throws ForbiddenException (layer-two write)")]
        public void Save_ModifiedRow_OutOfScope_ThrowsForbidden()
        {
            // The Update grant passes layer one, but the target record is out of scope (the authoritative re-query returns false), so layer two blocks it.
            var repo = new StubRepo { InScope = false };
            var bo = Bo(PermissionActions.Update, repo);
            Assert.Throws<ForbiddenException>(() => bo.Save(new SaveArgs { DataSet = ModifiedRowDataSet() }));
        }

        [Fact]
        [DisplayName("Save with a Modified row whose record is in scope (ExistsInScope=true) passes")]
        public void Save_ModifiedRow_InScope_PassesGate()
        {
            var repo = new StubRepo { InScope = true };
            var bo = Bo(PermissionActions.Update, repo);
            var ex = Record.Exception(() => bo.Save(new SaveArgs { DataSet = ModifiedRowDataSet() }));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("Save that edits only the detail rows (master Unchanged) counts as Update and throws ForbiddenException out of scope")]
        public void Save_DetailOnlyEdit_OutOfScope_ThrowsForbidden()
        {
            // Master Unchanged and detail Modified is still an edit of that existing record, so it goes through the layer-two Update check.
            var repo = new StubRepo { InScope = false };
            var bo = Bo(PermissionActions.Update, repo);
            Assert.Throws<ForbiddenException>(() => bo.Save(new SaveArgs { DataSet = DetailOnlyEditDataSet() }));
        }

        [Fact]
        [DisplayName("Save that omits the master table and sends only detail rows throws ForbiddenException (layer-two bypass)")]
        public void Save_DetailRowsWithoutMasterTable_ThrowsForbidden()
        {
            // `InScope=true`: even though the record scope check would pass, this payload shape has no master to check,
            // so the block is structural rather than about scope. Only true proves that.
            var repo = new StubRepo { InScope = true };
            var bo = Bo(PermissionActions.Update, repo);

            Assert.Throws<ForbiddenException>(
                () => bo.Save(new SaveArgs { DataSet = DetailRowsWithoutMasterTableDataSet() }));
        }

        [Theory]
        [InlineData(DataRowState.Added)]
        [InlineData(DataRowState.Modified)]
        [DisplayName("Save with a detail row pointing to a master the payload does not carry throws ForbiddenException")]
        public void Save_DetailOwnedByAbsentMaster_ThrowsForbidden(DataRowState state)
        {
            var repo = new StubRepo { InScope = true };
            var bo = Bo(PermissionActions.Create | PermissionActions.Update, repo);

            Assert.Throws<ForbiddenException>(
                () => bo.Save(new SaveArgs { DataSet = MasterWithDetailOwnedByAbsentMaster(state) }));
        }

        [Fact]
        [DisplayName("Save that reparents a detail row from someone else's master to its own throws ForbiddenException (the Original version)")]
        public void Save_DetailReparentedFromAbsentMaster_ThrowsForbidden()
        {
            var repo = new StubRepo { InScope = true };
            var bo = Bo(PermissionActions.Update, repo);

            Assert.Throws<ForbiddenException>(
                () => bo.Save(new SaveArgs { DataSet = MasterWithDetailReparentedFromAbsentMaster() }));
        }

        [Fact]
        [DisplayName("Control case: a well-formed detail edit, with the master present and the detail pointing to it, passes")]
        public void Save_WellFormedDetailEdit_PassesGate()
        {
            // Without this one, the tests above could all be satisfied by rejecting everything.
            var repo = new StubRepo { InScope = true };
            var bo = Bo(PermissionActions.Update, repo);

            var ex = Record.Exception(() => bo.Save(new SaveArgs { DataSet = WellFormedDetailEditDataSet() }));

            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("The gate is skipped when FormSchema declares no PermissionModelId (backward compatible)")]
        public void EmptyPermissionModelId_SkipsGate()
        {
            // Employee has no PermissionModelId, so even with Can denying everything the gate does not check and passes.
            var bo = Bo(PermissionActions.None, new StubRepo(), UngatedProgId);

            var ex = Record.Exception(() => bo.GetList(new GetListArgs()));

            Assert.Null(ex);
        }

        private sealed class FakeAuth : ICompanyAuthorizationService
        {
            private readonly PermissionActions _allowed;
            public FakeAuth(PermissionActions allowed) { _allowed = allowed; }
            public bool Can(Guid accessToken, string modelId, PermissionActions action) => _allowed.HasFlag(action);
        }

        /// <summary>
        /// Create is unrestricted; every other action returns a restricted scope, so write checks reach
        /// <see cref="StubRepo.ExistsInScope"/>, whose verdict each test sets.
        /// </summary>
        /// <remarks>
        /// The restricted scope is an empty AND group: non-null, so the database check runs, and true for every
        /// in-memory row, so the new-value check never decides these tests. The test session has no roles, and the
        /// real resolver would deny every scope.
        /// </remarks>
        private sealed class StubScopeResolver : IScopeResolver
        {
            public FilterNode? ResolveFilter(Guid accessToken, string modelId, PermissionActions action, Polhem.Definition.Forms.FormSchema formSchema)
                => action == PermissionActions.Create ? null : new FilterGroup();
        }

        private sealed class FakeFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _repo;
            public FakeFactory(IDataFormRepository repo) { _repo = repo; }
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_repo;
            public T Create<T>(Guid accessToken = default) where T : class => throw new NotSupportedException();
        }

        private sealed class StubRepo : IDataFormRepository
        {
            // Configurable authoritative in-scope verdict for write-scope tests; defaults to in-scope.
            public bool InScope { get; set; } = true;

            public DataFormListResult GetList(string selectFields, FilterNode? filter, SortFieldCollection? sortFields, PagingOptions? paging = null)
                => new() { Table = new DataTable() };
            public DataSet GetNewData(string timeZoneId = "") => new();
            public DataSet? GetData(Guid rowId, FilterNode? scopeFilter = null) => new();
            public DataTable GetRowsByRowId(string tableName, string selectFields, IReadOnlyCollection<Guid> rowIds) => new();
            public (DataSet? Refreshed, Dictionary<string, int> AffectedRows) Save(DataSet dataSet) => (dataSet, new Dictionary<string, int>());
            public int Delete(Guid rowId, FilterNode? scopeFilter = null) => 1;
            public bool ExistsInScope(Guid rowId, FilterNode? scopeFilter) => InScope;
        }
    }
}
