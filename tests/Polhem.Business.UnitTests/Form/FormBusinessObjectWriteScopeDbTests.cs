using System.ComponentModel;
using System.Data;
using Polhem.Base;
using Polhem.Base.Data;
using Polhem.Base.Exceptions;
using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// The detail-ownership check of the save path against each real database: a detail row of someone else's record,
    /// carried under the caller's own in-scope master, is neither rewritten nor deleted.
    /// </summary>
    /// <remarks>
    /// The check reads the detail rows' stored owners by their Original rowids through the same parameterized
    /// <c>IN</c> query on every provider, so this runs once per provider rather than trusting one of them for all.
    /// The record scope is the <c>Own</c> shape on a Guid owner column, which also exercises the authoritative master
    /// check against each database.
    /// </remarks>
    public class FormBusinessObjectWriteScopeDbTests : IClassFixture<SharedDbFixture>
    {
        private const string OwnerField = "owner_rowid";

        private static readonly Guid s_me = Guid.NewGuid();
        private static readonly Guid s_someoneElse = Guid.NewGuid();

        private readonly SharedDbFixture _fx;

        public FormBusinessObjectWriteScopeDbTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a foreign detail row under the caller's master is refused and left untouched, an own one is saved")]
        public void Save_SqlServer_DetailOwnershipBoundToDatabase() => Run(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a foreign detail row under the caller's master is refused and left untouched, an own one is saved")]
        public void Save_PostgreSql_DetailOwnershipBoundToDatabase() => Run(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a foreign detail row under the caller's master is refused and left untouched, an own one is saved")]
        public void Save_MySql_DetailOwnershipBoundToDatabase() => Run(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a foreign detail row under the caller's master is refused and left untouched, an own one is saved")]
        public void Save_Sqlite_DetailOwnershipBoundToDatabase() => Run(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a foreign detail row under the caller's master is refused and left untouched, an own one is saved")]
        public void Save_Oracle_DetailOwnershipBoundToDatabase() => Run(DatabaseType.Oracle);

        private void Run(DatabaseType databaseType)
        {
            string master = TransientForm.NewTableName("wsm_");
            string detail = TransientForm.NewTableName("wsd_");
            var form = new TransientForm(_fx, databaseType, BuildSchema(master, detail));
            form.CreateTables();
            try
            {
                var mine = Guid.NewGuid();
                var theirs = Guid.NewGuid();
                var myDetail = Guid.NewGuid();
                var theirDetail = Guid.NewGuid();
                InsertMaster(form, master, mine, s_me);
                InsertMaster(form, master, theirs, s_someoneElse);
                InsertDetail(form, detail, myDetail, mine);
                InsertDetail(form, detail, theirDetail, theirs);

                var bo = new FormBusinessObject(
                    form.CreateContext(
                        (typeof(ICompanyAuthorizationService), new AllowAll()),
                        (typeof(IScopeResolver), new OwnScopeResolver())),
                    TestSessionFactory.CreateAccessToken(_fx), master);

                Assert.Throws<ForbiddenException>(
                    () => bo.Save(new SaveArgs { DataSet = Payload(master, detail, mine, theirDetail, delete: false) }));
                Assert.Throws<ForbiddenException>(
                    () => bo.Save(new SaveArgs { DataSet = Payload(master, detail, mine, theirDetail, delete: true) }));
                Assert.Equal(1, StoredQty(form, detail, theirDetail));

                bo.Save(new SaveArgs { DataSet = Payload(master, detail, mine, myDetail, delete: false) });
                Assert.Equal(99, StoredQty(form, detail, myDetail));
            }
            finally
            {
                form.DropTables();
            }
        }

        /// <summary>
        /// The caller's own master, unchanged, with one detail row claiming it as owner in both versions.
        /// </summary>
        private static DataSet Payload(string master, string detail, Guid masterRowId, Guid detailRowId, bool delete)
        {
            var ds = new DataSet();
            var masterTable = ds.Tables.Add(master);
            masterTable.Columns.Add(SysFields.RowId, typeof(Guid));
            masterTable.Columns.Add("sys_id", typeof(string));
            masterTable.Columns.Add(OwnerField, typeof(Guid));
            masterTable.Rows.Add(masterRowId, "M", s_me);

            var detailTable = ds.Tables.Add(detail);
            detailTable.Columns.Add(SysFields.RowId, typeof(Guid));
            detailTable.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detailTable.Columns.Add("qty", typeof(int));
            var row = detailTable.Rows.Add(detailRowId, masterRowId, 1);

            ds.AcceptChanges();
            if (delete) { row.Delete(); }
            else { row["qty"] = 99; }
            return ds;
        }

        private static FormSchema BuildSchema(string master, string detail)
        {
            var schema = new FormSchema(master, "Write scope") { CategoryId = TransientForm.CategoryId, PermissionModelId = "WsDbModel" };
            var masterTable = schema.Tables!.Add(master, "Write scope");
            masterTable.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            masterTable.Fields!.Add(new FormField("sys_id", "ID", FieldDbType.String) { MaxLength = 20 });
            masterTable.Fields!.Add(OwnerField, "Owner", FieldDbType.Guid).ScopeRole = ScopeRole.Owner;

            var detailTable = schema.Tables.Add(detail, "Write scope item");
            detailTable.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            detailTable.Fields!.Add(SysFields.MasterRowId, "Master Row ID", FieldDbType.Guid);
            detailTable.Fields!.Add("qty", "Qty", FieldDbType.Integer);
            return schema;
        }

        private static void InsertMaster(TransientForm form, string table, Guid rowId, Guid owner)
            => form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(table)} ({form.Quote(SysFields.RowId)}, {form.Quote("sys_id")}, {form.Quote(OwnerField)}) " +
                "VALUES ({0}, {1}, {2})",
                rowId, "M" + rowId.ToString("N")[..6], owner);

        private static void InsertDetail(TransientForm form, string table, Guid rowId, Guid masterRowId)
            => form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(table)} ({form.Quote(SysFields.RowId)}, {form.Quote(SysFields.MasterRowId)}, {form.Quote("qty")}) " +
                "VALUES ({0}, {1}, {2})",
                rowId, masterRowId, 1);

        private static int StoredQty(TransientForm form, string table, Guid rowId)
        {
            var stored = form.Repository.GetRowsByRowId(table, "qty", [rowId]);
            return ValueUtilities.CInt(Assert.Single(stored.Rows.Cast<DataRow>())["qty"]);
        }

        private sealed class AllowAll : ICompanyAuthorizationService
        {
            public bool Can(Guid accessToken, string modelId, PermissionAction action) => true;
        }

        /// <summary>The <c>Own</c> strategy for every action: the owner column must be the caller.</summary>
        private sealed class OwnScopeResolver : IScopeResolver
        {
            public FilterNode? ResolveFilter(Guid accessToken, string modelId, PermissionAction action, FormSchema formSchema)
                => FilterCondition.In(OwnerField, [s_me]);
        }
    }
}
