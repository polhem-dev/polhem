using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Two companies sharing one company database through <see cref="Polhem.Repository.Form.DataFormRepository"/>: neither
    /// reads, changes nor deletes the other's rows, and every row it saves carries its own company.
    /// </summary>
    /// <remarks>
    /// Company A is the form's own repository (<see cref="TransientForm.CompanyId"/>); company B is a
    /// second repository over the same tables. Each case writes A's record first and then acts as B.
    /// </remarks>
    public class DataFormRepositoryCompanyIsolationTests : IClassFixture<SharedDbFixture>
    {
        private const string OtherCompanyId = "c002";
        private const string NoteColumn = "note";
        private const string QtyColumn = "qty";
        private readonly SharedDbFixture _fx;

        public DataFormRepositoryCompanyIsolationTests(SharedDbFixture fx) { _fx = fx; }

        public enum Case
        {
            ListHidesOtherCompany,
            CountHidesOtherCompany,
            GetDataReturnsNullForOtherCompany,
            SaveStampsOwnCompany,
            UpdateOfOtherCompanyRowChangesNothing,
            DeleteOfOtherCompanyRowDeletesNothing,
            SameNumberInTwoCompanies,
        }

        [DbTheory(DatabaseType.SQLServer)]
        [MemberData(nameof(Cases))]
        [DisplayName("SQL Server: companies sharing a database stay apart through the form repository")]
        public void Isolation_SqlServer(Case testCase) => Run(DatabaseType.SQLServer, testCase);

        [DbTheory(DatabaseType.SQLite)]
        [MemberData(nameof(Cases))]
        [DisplayName("SQLite: companies sharing a database stay apart through the form repository")]
        public void Isolation_Sqlite(Case testCase) => Run(DatabaseType.SQLite, testCase);

        [DbTheory(DatabaseType.PostgreSQL)]
        [MemberData(nameof(Cases))]
        [DisplayName("PostgreSQL: companies sharing a database stay apart through the form repository")]
        public void Isolation_PostgreSql(Case testCase) => Run(DatabaseType.PostgreSQL, testCase);

        [DbTheory(DatabaseType.MySQL)]
        [MemberData(nameof(Cases))]
        [DisplayName("MySQL: companies sharing a database stay apart through the form repository")]
        public void Isolation_MySql(Case testCase) => Run(DatabaseType.MySQL, testCase);

        [DbTheory(DatabaseType.Oracle)]
        [MemberData(nameof(Cases))]
        [DisplayName("Oracle: companies sharing a database stay apart through the form repository")]
        public void Isolation_Oracle(Case testCase) => Run(DatabaseType.Oracle, testCase);

        public static TheoryData<Case> Cases => [.. Enum.GetValues<Case>()];

        private void Run(DatabaseType databaseType, Case testCase)
        {
            string master = TransientForm.NewTableName("tb_iso_");
            var form = new TransientForm(_fx, databaseType, BuildSchema(master));
            form.CreateTables();
            try
            {
                var companyA = form.Repository;
                var companyB = form.CreateRepository(OtherCompanyId);
                var rowA = SaveNew(companyA, "A1", "from A");

                switch (testCase)
                {
                    case Case.ListHidesOtherCompany:
                        Assert.Empty(companyB.GetList(string.Empty, null, null).Table!.Rows);
                        Assert.Single(companyA.GetList(string.Empty, null, null).Table!.Rows);
                        break;

                    case Case.CountHidesOtherCompany:
                        var page = companyB.GetList(string.Empty, null, null,
                            new Polhem.Definition.Paging.PagingOptions { Page = 1, PageSize = 10, IncludeTotalCount = true });
                        Assert.Equal(0, page.Paging!.TotalCount);
                        break;

                    case Case.GetDataReturnsNullForOtherCompany:
                        Assert.Null(companyB.GetData(rowA));
                        break;

                    case Case.SaveStampsOwnCompany:
                        Assert.Equal(TransientForm.CompanyId, StoredCompany(form, master, rowA));
                        Assert.Equal(TransientForm.CompanyId, StoredCompany(form, DetailName(master), DetailRowOf(form, master, rowA)));
                        break;

                    case Case.UpdateOfOtherCompanyRowChangesNothing:
                        var record = companyA.GetData(rowA)!;
                        record.Tables[master]!.Rows[0][NoteColumn] = "from B";
                        _ = Record.Exception(() => companyB.Save(record));
                        Assert.Equal("from A", StoredNote(form, master, rowA));
                        break;

                    case Case.DeleteOfOtherCompanyRowDeletesNothing:
                        Assert.Equal(0, companyB.Delete(rowA));
                        Assert.NotNull(companyA.GetData(rowA));
                        Assert.Single(companyA.GetData(rowA)!.Tables[DetailName(master)]!.Rows);
                        break;

                    case Case.SameNumberInTwoCompanies:
                        var rowB = SaveNew(companyB, "A1", "from B");
                        Assert.Equal("from B", ValueUtilities.CStr(companyB.GetData(rowB)!.Tables[master]!.Rows[0][NoteColumn]));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(testCase), testCase, null);
                }
            }
            finally
            {
                form.DropTables();
            }
        }

        /// <summary>
        /// Saves a new master with one detail row; the master's data table also carries a
        /// <see cref="SysFields.CompanyId"/> column naming the other company, which the save must ignore.
        /// </summary>
        private static Guid SaveNew(Polhem.Repository.Abstractions.Form.IDataFormRepository repository, string number, string note)
        {
            var dataSet = repository.GetNewData();
            var masterTable = dataSet.Tables[0];
            var masterRow = masterTable.Rows[0];
            masterRow[SysFields.Id] = number;
            masterRow[NoteColumn] = note;
            masterTable.Columns.Add(SysFields.CompanyId, typeof(string));
            masterRow[SysFields.CompanyId] = "intruder";
            var rowId = ValueUtilities.CGuid(masterRow[SysFields.RowId]);

            var detailTable = dataSet.Tables[1];
            var detailRow = detailTable.NewRow();
            detailRow[SysFields.RowId] = Guid.NewGuid();
            detailRow[SysFields.MasterRowId] = rowId;
            detailRow[QtyColumn] = 1;
            detailTable.Rows.Add(detailRow);

            repository.Save(dataSet);
            return rowId;
        }

        private static FormSchema BuildSchema(string master)
        {
            var schema = new FormSchema(master, "Isolation") { CategoryId = TransientForm.CategoryId };
            var masterTable = schema.Tables!.Add(master, "Isolation");
            masterTable.Fields!.Add(SysFields.No, "No", FieldDbType.AutoIncrement);
            masterTable.Fields.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            masterTable.Fields!.Add(new FormField(SysFields.Id, "Number", FieldDbType.String) { MaxLength = 20 });
            masterTable.Fields!.Add(new FormField(NoteColumn, "Note", FieldDbType.String) { MaxLength = 50 });

            var detailTable = schema.Tables.Add(DetailName(master), "Isolation lines");
            detailTable.Fields!.Add(SysFields.No, "No", FieldDbType.AutoIncrement);
            detailTable.Fields.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            detailTable.Fields.Add(SysFields.MasterRowId, "Master", FieldDbType.Guid);
            detailTable.Fields.Add(QtyColumn, "Qty", FieldDbType.Integer);
            return schema;
        }

        private static string DetailName(string master) => master + "_d";

        private static string? StoredCompany(TransientForm form, string table, Guid rowId)
            => ValueUtilities.CStr(form.DbAccess.ExecuteScalar(
                $"SELECT {form.Quote(SysFields.CompanyId)} FROM {form.Quote(table)} WHERE {form.Quote(SysFields.RowId)} = {{0}}", rowId) ?? DBNull.Value);

        private static string? StoredNote(TransientForm form, string table, Guid rowId)
            => ValueUtilities.CStr(form.DbAccess.ExecuteScalar(
                $"SELECT {form.Quote(NoteColumn)} FROM {form.Quote(table)} WHERE {form.Quote(SysFields.RowId)} = {{0}}", rowId) ?? DBNull.Value);

        private static Guid DetailRowOf(TransientForm form, string master, Guid masterRowId)
            => ValueUtilities.CGuid(form.DbAccess.ExecuteScalar(
                $"SELECT {form.Quote(SysFields.RowId)} FROM {form.Quote(DetailName(master))} " +
                $"WHERE {form.Quote(SysFields.MasterRowId)} = {{0}}", masterRowId) ?? DBNull.Value);
    }
}
