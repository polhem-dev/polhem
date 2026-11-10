using System.ComponentModel;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// <see cref="ICompanyRowAssignmentRepository"/> finds a company table's rows that belong to no company and gives
    /// them one, on every provider.
    /// </summary>
    /// <remarks>
    /// The rows are written with an empty company, which Oracle stores as NULL, so the same test covers both of the
    /// ways a row can belong to no company.
    /// </remarks>
    public class CompanyRowAssignmentRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private const string Company = "assigned";

        private readonly SharedDbFixture _fx;

        public CompanyRowAssignmentRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: rows with no company are counted, given the company, and then no longer counted")]
        public void Assign_SqlServer() => Run(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: rows with no company are counted, given the company, and then no longer counted")]
        public void Assign_PostgreSql() => Run(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: rows with no company are counted, given the company, and then no longer counted")]
        public void Assign_MySql() => Run(DatabaseType.MySQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: rows with no company are counted, given the company, and then no longer counted")]
        public void Assign_Sqlite() => Run(DatabaseType.SQLite);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: rows with no company, stored as NULL, are counted, given the company, and then no longer counted")]
        public void Assign_Oracle() => Run(DatabaseType.Oracle);

        private void Run(DatabaseType databaseType)
        {
            string table = TransientForm.NewTableName("cra_");
            var form = new TransientForm(_fx, databaseType, BuildSchema(table));
            form.CreateTables();
            try
            {
                Insert(form, table, Guid.NewGuid(), string.Empty);
                Insert(form, table, Guid.NewGuid(), string.Empty);
                var owned = Guid.NewGuid();
                Insert(form, table, owned, "other");

                var repository = TestRepositoryFactory
                    .Create(_fx.GetRequiredService<IDefineAccess>(), _fx.GetRequiredService<IDbConnectionManager>())
                    .Create<ICompanyRowAssignmentRepository>();

                Assert.Equal(2, repository.CountUnassignedRows(form.DatabaseId, table, hasCompanyColumn: true));
                Assert.Equal(3, repository.CountUnassignedRows(form.DatabaseId, table, hasCompanyColumn: false));

                repository.AssignUnassignedRows(form.DatabaseId, table, Company);

                Assert.Equal(0, repository.CountUnassignedRows(form.DatabaseId, table, hasCompanyColumn: true));
                Assert.Equal(2, CountOf(form, table, Company));
                // A row that already had a company keeps it.
                Assert.Equal(1, CountOf(form, table, "other"));
            }
            finally
            {
                form.DropTables();
            }
        }

        private static FormSchema BuildSchema(string table)
        {
            var schema = new FormSchema(table, "Assignment") { CategoryId = TransientForm.CategoryId };
            var fields = schema.Tables!.Add(table, "Assignment").Fields!;
            fields.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            return schema;
        }

        private static void Insert(TransientForm form, string table, Guid rowId, string companyId)
            => form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(table)} ({form.Quote(SysFields.RowId)}, {form.Quote(SysFields.CompanyId)}) VALUES ({{0}}, {{1}})",
                rowId, companyId);

        private static int CountOf(TransientForm form, string table, string companyId)
            => ValueUtilities.CInt(form.DbAccess.ExecuteScalar(
                $"SELECT COUNT(*) FROM {form.Quote(table)} WHERE {form.Quote(SysFields.CompanyId)} = {{0}}", companyId)!);
    }
}
