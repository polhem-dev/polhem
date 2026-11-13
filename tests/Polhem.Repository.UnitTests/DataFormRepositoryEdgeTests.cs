using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Sorting;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// <see cref="DataFormRepository"/> on the shapes the main suites do not reach: construction guards, schemas
    /// without a master table or a sortable key, default values that cannot convert, a form outside the company
    /// scope, and the row-id and scope reads.
    /// </summary>
    public class DataFormRepositoryEdgeTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;

        public DataFormRepositoryEdgeTests(SharedDbFixture fx) { _fx = fx; }

        // -------- Construction --------

        [Fact]
        [DisplayName("The definition-loading constructor refuses a null context")]
        public void Constructor_NullContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DataFormRepository(null!, Guid.Empty, "Any"));
        }

        [Fact]
        [DisplayName("The explicit-company constructor refuses a null schema")]
        public void Constructor_NullSchema_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DataFormRepository(TestRepositoryContext.Create(), "Any", null!, "db", "co1"));
        }

        [Fact]
        [DisplayName("A company form built without a company is refused")]
        public void Constructor_CompanyFormWithoutCompany_Throws()
        {
            var schema = Schema("EdgeCo", categoryId: DbCategoryIds.Company);

            Assert.Throws<InvalidOperationException>(() =>
                new DataFormRepository(TestRepositoryContext.Create(), "EdgeCo", schema, "db"));
        }

        // -------- Without a database --------

        [Fact]
        [DisplayName("GetNewData refuses a schema without a master table")]
        public void GetNewData_NoMasterTable_Throws()
        {
            var schema = new FormSchema("EdgeNoMaster", "No master") { CategoryId = DbCategoryIds.Common };

            Assert.Throws<InvalidOperationException>(() => Repo(schema).GetNewData());
        }

        [Fact]
        [DisplayName("GetNewData ignores a default that cannot convert to its column type, and skips virtual fields")]
        public void GetNewData_UnconvertibleDefaults_AreIgnored()
        {
            var schema = Schema("EdgeDefaults");
            var master = schema.MasterTable!;
            master.Fields!.Add("due_on", "Due on", FieldDbType.Date).DefaultValue = "not-a-date";
            master.Fields.Add("plain_on", "Plain on", FieldDbType.Date);
            master.Fields.Add("ref_guid", "Ref", FieldDbType.Guid).DefaultValue = "not-a-guid";
            master.Fields!.Add(new FormField("calc", "Calc", FieldDbType.Decimal, FieldType.VirtualField));
            var detail = schema.Tables!.Add("EdgeDefaults_Item", "Item");
            detail.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            detail.Fields!.Add(new FormField("detail_calc", "Calc", FieldDbType.Decimal, FieldType.VirtualField));

            var dataSet = Repo(schema).GetNewData();

            var row = dataSet.Tables["EdgeDefaults"]!.Rows[0];
            Assert.Equal(row["plain_on"], row["due_on"]);
            Assert.Equal(Guid.Empty, row["ref_guid"]);
            Assert.False(dataSet.Tables["EdgeDefaults"]!.Columns.Contains("calc"));
            Assert.False(dataSet.Tables["EdgeDefaults_Item"]!.Columns.Contains("detail_calc"));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("99999999999")]
        [DisplayName("GetNewData keeps the column's own default for an integer default that cannot convert, instead of throwing")]
        public void GetNewData_UnconvertibleIntegerDefault_KeepsColumnDefault(string defaultValue)
        {
            var schema = Schema("EdgeIntDefault");
            schema.MasterTable!.Fields!.Add("qty", "Quantity", FieldDbType.Integer).DefaultValue = defaultValue;

            var dataSet = Repo(schema).GetNewData();

            var table = dataSet.Tables["EdgeIntDefault"]!;
            Assert.Equal(table.Columns["qty"]!.DefaultValue, table.Rows[0]["qty"]);
        }

        [Fact]
        [DisplayName("GetRowsByRowId refuses a table the form does not declare")]
        public void GetRowsByRowId_UnknownTable_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                Repo(Schema("EdgeRows")).GetRowsByRowId("NoSuchTable", string.Empty, [Guid.NewGuid()]));
        }

        [Fact]
        [DisplayName("GetRowsByRowId with no row ids returns an empty table without reading")]
        public void GetRowsByRowId_NoRowIds_ReturnsEmpty()
        {
            var table = Repo(Schema("EdgeRows")).GetRowsByRowId("EdgeRows", string.Empty, []);

            Assert.Equal("EdgeRows", table.TableName);
            Assert.Empty(table.Rows);
        }

        [Fact]
        [DisplayName("A paging sort on a master with sys_rowid but no sys_no gets sys_rowid appended as the tie-breaker")]
        public void ResolvePagingSort_RowIdOnly_AppendsRowId()
        {
            var schema = Schema("EdgeSort");

            var sort = DataFormRepository.ResolvePagingSort(schema, [new SortField("sys_id", SortDirection.Desc)]);

            Assert.Equal(["sys_id", SysFields.RowId], sort.Select(f => f.FieldName));
            Assert.Equal(SortDirection.Desc, sort[0].Direction);
        }

        [Fact]
        [DisplayName("A paging sort that already names the unique key is used as given")]
        public void ResolvePagingSort_AlreadyUnique_ReturnsSame()
        {
            var sortFields = new SortFieldCollection { new SortField(SysFields.RowId, SortDirection.Asc) };

            Assert.Same(sortFields, DataFormRepository.ResolvePagingSort(Schema("EdgeSort"), sortFields));
        }

        [Fact]
        [DisplayName("A paging sort on a master without any unique key is used as given")]
        public void ResolvePagingSort_NoUniqueKey_ReturnsSame()
        {
            var schema = new FormSchema("EdgeNoKey", "No key") { CategoryId = DbCategoryIds.Common };
            schema.Tables!.Add("EdgeNoKey", "No key").Fields!.Add("sys_id", "ID", FieldDbType.String);
            var sortFields = new SortFieldCollection { new SortField("sys_id", SortDirection.Asc) };

            Assert.Same(sortFields, DataFormRepository.ResolvePagingSort(schema, sortFields));
        }

        [Fact]
        [DisplayName("A paging sort on a schema without a master table is used as given")]
        public void ResolvePagingSort_NoMasterTable_ReturnsSame()
        {
            var schema = new FormSchema("EdgeNoMaster", "No master") { CategoryId = DbCategoryIds.Common };
            var sortFields = new SortFieldCollection { new SortField("sys_id", SortDirection.Asc) };

            Assert.Same(sortFields, DataFormRepository.ResolvePagingSort(schema, sortFields));
        }

        [Fact]
        [DisplayName("Without a sort, paging needs sys_no and refuses a master that lacks it")]
        public void ResolvePagingSort_NoSortWithoutSysNo_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => DataFormRepository.ResolvePagingSort(Schema("EdgeSort"), null));
        }

        [Fact]
        [DisplayName("Without a sort, paging refuses a schema without a master table")]
        public void ResolvePagingSort_NoSortWithoutMaster_Throws()
        {
            var schema = new FormSchema("EdgeNoMaster", "No master") { CategoryId = DbCategoryIds.Common };

            Assert.Throws<InvalidOperationException>(() => DataFormRepository.ResolvePagingSort(schema, []));
        }

        // -------- SQLite --------

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: Save refuses a schema without a master table")]
        public void Save_Sqlite_NoMasterTable_Throws()
        {
            var schema = new FormSchema("EdgeNoMaster", "No master") { CategoryId = DbCategoryIds.Common };
            var repo = new DataFormRepository(
                TestRepositoryContext.Create(_fx.GetRequiredService<Polhem.Db.Manager.IDbConnectionManager>()),
                schema.ProgId, schema, TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company"));

            Assert.Throws<InvalidOperationException>(() => repo.Save(new DataSet()));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a form outside the company scope saves and reads back its row")]
        public void Save_Sqlite_NonCompanyForm_InsertsAndReadsBack()
            => WithForm(DbCategoryIds.Common, (form, progId) =>
            {
                var dataSet = form.Repository.GetNewData();
                var rowId = (Guid)dataSet.Tables[progId]!.Rows[0][SysFields.RowId];
                dataSet.Tables[progId]!.Rows[0]["sys_id"] = "A1";

                var (refreshed, affected) = form.Repository.Save(dataSet);

                Assert.Equal(1, affected[progId]);
                Assert.Equal("A1", refreshed!.Tables[progId]!.Rows[0]["sys_id"]);
                Assert.Equal(rowId, Polhem.Core.ValueUtilities.CGuid(refreshed.Tables[progId]!.Rows[0][SysFields.RowId]));
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetRowsByRowId adds sys_rowid to a field list that leaves it out, and keeps one that names it")]
        public void GetRowsByRowId_Sqlite_IncludesRowId()
            => WithForm(DbCategoryIds.Company, (form, progId) =>
            {
                var rowId = Insert(form, progId, "R1");

                var withoutRowId = form.Repository.GetRowsByRowId(progId, "sys_id", [rowId]);
                var withRowId = form.Repository.GetRowsByRowId(progId, "sys_rowid,sys_id", [rowId, rowId]);

                Assert.True(withoutRowId.Columns.Contains(SysFields.RowId));
                Assert.Equal("R1", Assert.Single(withoutRowId.Rows.Cast<DataRow>())["sys_id"]);
                Assert.Single(withRowId.Rows.Cast<DataRow>());
            });

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: ExistsInScope answers by the scope filter, and Delete outside the scope removes nothing")]
        public void ExistsInScopeAndDelete_Sqlite_HonourScope()
            => WithForm(DbCategoryIds.Company, (form, progId) =>
            {
                var rowId = Insert(form, progId, "S1");
                var inScope = FilterCondition.Equal("sys_id", "S1");
                var outOfScope = FilterCondition.Equal("sys_id", "other");

                Assert.True(form.Repository.ExistsInScope(rowId, inScope));
                Assert.False(form.Repository.ExistsInScope(rowId, outOfScope));
                Assert.Equal(0, form.Repository.Delete(rowId, outOfScope));
                Assert.NotNull(form.Repository.GetData(rowId));
                Assert.Equal(1, form.Repository.Delete(rowId, inScope));
                Assert.Null(form.Repository.GetData(rowId));
            });

        // -------- Fixtures --------

        private static DataFormRepository Repo(FormSchema schema)
            => new(TestRepositoryContext.Create(), schema.ProgId, schema, "unused-db");

        private static FormSchema Schema(string progId, string categoryId = DbCategoryIds.Common)
        {
            var schema = new FormSchema(progId, progId) { CategoryId = categoryId };
            var master = schema.Tables!.Add(progId, progId);
            master.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            master.Fields.Add("sys_id", "ID", FieldDbType.String);
            return schema;
        }

        private void WithForm(string categoryId, Action<TransientForm, string> body)
        {
            string progId = TransientForm.NewTableName("tb_edge_");
            var schema = Schema(progId, categoryId);
            var form = new TransientForm(_fx, DatabaseType.SQLite, schema);
            form.CreateTables();
            try
            {
                body(form, progId);
            }
            finally
            {
                form.DropTables();
            }
        }

        private static Guid Insert(TransientForm form, string progId, string sysId)
        {
            var rowId = Guid.NewGuid();
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(progId)} ({form.Quote(SysFields.RowId)}, {form.Quote("sys_id")}, " +
                $"{form.Quote(SysFields.CompanyId)}) VALUES ({{0}}, {{1}}, {{2}})",
                rowId, sysId, TransientForm.CompanyId);
            return rowId;
        }
    }
}
