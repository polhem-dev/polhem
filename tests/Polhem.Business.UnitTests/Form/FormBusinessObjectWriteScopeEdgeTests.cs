using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The less common payload shapes on the save path's record-scope checks: payloads without a master table, tables
    /// the schema does not declare, detail tables without the columns the checks key on, and resolvers that leave an
    /// action unrestricted.
    /// </summary>
    public class FormBusinessObjectWriteScopeEdgeTests : IClassFixture<PolhemTestFixture>
    {
        private const string ProgId = "WseForm";
        private const string DetailTable = "WseForm_Item";
        private const string NoteTable = "WseForm_Note";
        private const string OwnerField = "owner_rowid";

        private static readonly Guid s_me = Guid.NewGuid();

        private readonly PolhemTestFixture _fx;

        public FormBusinessObjectWriteScopeEdgeTests(PolhemTestFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("A payload without the master table and without pending rows saves without a scope check")]
        public void Save_NoMasterTableAndNoPendingRows_Saves()
        {
            var repo = new StubFormRepository();
            var ds = new DataSet();
            var detail = AddDetailTable(ds);
            detail.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), 1);
            ds.AcceptChanges();

            Bo(repo).Save(new SaveArgs { DataSet = ds });

            Assert.Empty(repo.ScopeChecks);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("A form whose schema declares no master table skips both record-scope checks")]
        public void Save_SchemaWithoutMasterTable_SkipsScopeChecks()
        {
            var repo = new StubFormRepository();
            var schema = new FormSchema(ProgId, "No master") { CategoryId = "company", PermissionModelId = "WseModel" };
            var ds = new DataSet();
            var table = ds.Tables.Add(ProgId);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Rows.Add(Guid.NewGuid());

            Bo(repo, schema).Save(new SaveArgs { DataSet = ds });

            Assert.Empty(repo.ScopeChecks);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("Two modified masters resolve the Update scope once per check, not once per row")]
        public void Save_TwoModifiedMasters_ResolvesScopeOncePerCheck()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var repo = new StubFormRepository { InScope = { first, second } };
            var resolver = new CountingResolver(FilterCondition.In(OwnerField, [s_me]));
            var ds = NewDataSet();
            var a = AddMaster(ds, first);
            var b = AddMaster(ds, second);
            ds.AcceptChanges();
            a["sys_id"] = "a2";
            b["sys_id"] = "b2";

            Bo(repo, resolver: resolver).Save(new SaveArgs { DataSet = ds });

            Assert.Equal([first, second], repo.ScopeChecks);
            Assert.Equal(2, resolver.Calls[PermissionActions.Update]);
        }

        [Fact]
        [DisplayName("An action the resolver leaves unrestricted needs no stored-row check and no new-value check")]
        public void Save_UnrestrictedScope_SkipsRowChecks()
        {
            var rowId = Guid.NewGuid();
            var repo = new StubFormRepository();
            var ds = NewDataSet();
            var row = AddMaster(ds, rowId);
            ds.AcceptChanges();
            row[OwnerField] = Guid.NewGuid();

            Bo(repo, resolver: new CountingResolver(null)).Save(new SaveArgs { DataSet = ds });

            Assert.Empty(repo.ScopeChecks);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("A payload table the schema does not declare is checked for its claimed owner but not against stored rows")]
        public void Save_UndeclaredTable_SkipsStoredOwnership()
        {
            var master = Guid.NewGuid();
            var repo = new StubFormRepository { InScope = { master } };
            var ds = NewDataSet();
            AddMaster(ds, master);
            var extra = ds.Tables.Add("WseForm_Extra");
            extra.Columns.Add(SysFields.RowId, typeof(Guid));
            extra.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            var extraRow = extra.Rows.Add(Guid.NewGuid(), master);
            ds.AcceptChanges();
            extraRow[SysFields.MasterRowId] = master;

            var ex = Record.Exception(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("A modified detail table that carries no rowid column has no stored rows to verify")]
        public void Save_DetailWithoutRowIdColumn_SkipsStoredOwnership()
        {
            var master = Guid.NewGuid();
            var repo = new StubFormRepository { InScope = { master } };
            var ds = NewDataSet();
            AddMaster(ds, master);
            ds.Tables.Remove(DetailTable);
            var detail = ds.Tables.Add(DetailTable);
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Columns.Add("qty", typeof(int));
            var row = detail.Rows.Add(master, 1);
            ds.AcceptChanges();
            row["qty"] = 2;

            var ex = Record.Exception(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("A modified row of a detail table that declares no master-rowid field is refused")]
        public void Save_DetailTableWithoutMasterRowIdField_ThrowsForbidden()
        {
            var master = Guid.NewGuid();
            var repo = new StubFormRepository { InScope = { master } };
            var ds = NewDataSet();
            AddMaster(ds, master);
            var note = ds.Tables.Add(NoteTable);
            note.Columns.Add(SysFields.RowId, typeof(Guid));
            note.Columns.Add("memo");
            var row = note.Rows.Add(Guid.NewGuid(), "x");
            ds.AcceptChanges();
            row["memo"] = "y";

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.Empty(repo.Saved);
        }

        [Fact]
        [DisplayName("A modified detail row the database no longer holds is not refused, since its statement matches nothing")]
        public void Save_DetailNoLongerStored_Passes()
        {
            var master = Guid.NewGuid();
            var repo = new StubFormRepository { InScope = { master } };
            var ds = NewDataSet();
            AddMaster(ds, master);
            var detail = AddDetail(ds, Guid.NewGuid(), master);
            ds.AcceptChanges();
            detail["qty"] = 5;

            var ex = Record.Exception(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Single(repo.Saved);
        }

        [Fact]
        [DisplayName("Stored detail rows read back without a master-rowid column cannot be confirmed and are refused")]
        public void Save_StoredRowsWithoutMasterColumn_ThrowsForbidden()
        {
            var master = Guid.NewGuid();
            var detailRowId = Guid.NewGuid();
            var repo = new StubFormRepository
            {
                InScope = { master },
                RowsByRowId = (name, ids) =>
                {
                    var table = new DataTable(name);
                    table.Columns.Add(SysFields.RowId, typeof(Guid));
                    foreach (var id in ids) { table.Rows.Add(id); }
                    return table;
                },
            };
            var ds = NewDataSet();
            AddMaster(ds, master);
            var detail = AddDetail(ds, detailRowId, master);
            ds.AcceptChanges();
            detail["qty"] = 5;

            Assert.Throws<ForbiddenException>(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));
            Assert.Empty(repo.Saved);
        }

        [Fact]
        [DisplayName("An unchanged detail row pointing at another master is not written and so is not refused")]
        public void Save_UnchangedDetailWithForeignOwner_Passes()
        {
            var master = Guid.NewGuid();
            var repo = new StubFormRepository { InScope = { master } };
            var ds = NewDataSet();
            var masterRow = AddMaster(ds, master);
            AddDetail(ds, Guid.NewGuid(), Guid.NewGuid());
            ds.AcceptChanges();
            masterRow["sys_id"] = "changed";

            var ex = Record.Exception(() => Bo(repo).Save(new SaveArgs { DataSet = ds }));

            Assert.Null(ex);
            Assert.Equal([master], repo.ScopeChecks);
        }

        // -------- Fixtures --------

        private FormBusinessObject Bo(StubFormRepository repo, FormSchema? schema = null, IScopeResolver? resolver = null)
        {
            var defineAccess = new FormSchemaOverlayDefineAccess(
                _fx.GetRequiredService<IDefineAccess>(), schema ?? BuildSchema());
            var ctx = new BusinessObjectContext
            {
                DefineAccess = defineAccess,
                SessionInfoService = _fx.GetRequiredService<ISessionInfoService>(),
                LanguageService = _fx.GetRequiredService<ILanguageService>(),
                BoFactory = _fx.GetRequiredService<IBusinessObjectFactory>(),
                Services = new TestOverrideServiceProvider(_fx.Provider,
                [
                    (typeof(ICompanyAuthorizationService), new AllowAll()),
                    (typeof(IScopeResolver), resolver ?? new CountingResolver(FilterCondition.In(OwnerField, [s_me]))),
                    (typeof(IRepositoryFactory), new StubRepositoryFactory(repo)),
                ]),
            };
            return new FormBusinessObject(ctx, TestSessionFactory.CreateAccessToken(_fx), ProgId);
        }

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(ProgId, "Write scope edges") { CategoryId = "company", PermissionModelId = "WseModel" };
            var master = schema.Tables!.Add(ProgId, "Write scope edges");
            master.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            master.Fields.Add("sys_id", "ID", FieldDbType.String);
            master.Fields.Add(OwnerField, "Owner", FieldDbType.Guid).ScopeRole = ScopeRole.Owner;

            var detail = schema.Tables.Add(DetailTable, "Item");
            detail.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            detail.Fields.Add(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid);
            detail.Fields.Add("qty", "Qty", FieldDbType.Integer);

            var note = schema.Tables.Add(NoteTable, "Note");
            note.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            note.Fields.Add("memo", "Memo", FieldDbType.String);
            return schema;
        }

        private static DataSet NewDataSet()
        {
            var ds = new DataSet();
            var master = ds.Tables.Add(ProgId);
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add("sys_id");
            master.Columns.Add(OwnerField, typeof(Guid));
            AddDetailTable(ds);
            return ds;
        }

        private static DataTable AddDetailTable(DataSet ds)
        {
            var detail = ds.Tables.Add(DetailTable);
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Columns.Add("qty", typeof(int));
            return detail;
        }

        private static DataRow AddMaster(DataSet ds, Guid rowId)
            => ds.Tables[ProgId]!.Rows.Add(rowId, "m", s_me);

        private static DataRow AddDetail(DataSet ds, Guid rowId, Guid masterRowId)
            => ds.Tables[DetailTable]!.Rows.Add(rowId, masterRowId, 1);

        private sealed class AllowAll : ICompanyAuthorizationService
        {
            public bool Can(SessionCompanyScope companyScope, string modelId, PermissionActions action) => true;
            public bool Can(Guid accessToken, string modelId, PermissionActions action) => true;
        }

        private sealed class CountingResolver : IScopeResolver
        {
            private readonly FilterNode? _filter;
            public CountingResolver(FilterNode? filter) { _filter = filter; }

            public Dictionary<PermissionActions, int> Calls { get; } = [];

            public FilterNode? ResolveFilter(SessionCompanyScope companyScope, string modelId, PermissionActions action, FormSchema formSchema)
            {
                Calls[action] = Calls.GetValueOrDefault(action) + 1;
                return _filter;
            }

            public FilterNode? ResolveFilter(Guid accessToken, string modelId, PermissionActions action, FormSchema formSchema)
                => ResolveFilter(SessionCompanyScope.None, modelId, action, formSchema);
        }
    }
}
