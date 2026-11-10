using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// A <see cref="DbAccess"/> bound to a company fills <see cref="CommandTextVariable.CompanyId"/> with it
    /// and refuses a command that leaves the placeholder out; an unbound one refuses the placeholder.
    /// </summary>
    public class DbAccessCompanyBindingTests : IClassFixture<SharedDbFixture>
    {
        private const string CompanySql = "SELECT * FROM t WHERE sys_company_id = {@CompanyId} AND a = {0}";
        private readonly SharedDbFixture _fx;

        public DbAccessCompanyBindingTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("A bound command turns the company placeholder into a parameter carrying the company")]
        public void CreateCommand_Bound_FillsCompanyParameter()
        {
            var spec = new DbCommandSpec(DbCommandKind.DataTable, CompanySql, "x");
            using var conn = new SqlConnection();

            using var cmd = spec.CreateCommand(DatabaseType.SQLServer, conn, "c001");

            Assert.Equal("SELECT * FROM t WHERE sys_company_id = @polhem_company_id AND a = @p0", cmd.CommandText);
            Assert.Equal("c001", cmd.Parameters["@polhem_company_id"].Value);
            Assert.DoesNotContain(spec.Parameters, p => p.Name == DbCommandSpec.CompanyParameterName);
        }

        [Fact]
        [DisplayName("The company parameter takes the provider's prefix, a colon on Oracle")]
        public void CreateCommand_BoundOnOracle_UsesColonPrefix()
        {
            var spec = new DbCommandSpec(DbCommandKind.DataTable, CompanySql, "x");
            using var conn = new SqlConnection();

            using var cmd = spec.CreateCommand(DatabaseType.Oracle, conn, "c001");

            Assert.Contains("sys_company_id = :polhem_company_id", cmd.CommandText, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A bound command that does not reference the company placeholder is refused")]
        public void CreateCommand_BoundWithoutPlaceholder_Throws()
        {
            var spec = new DbCommandSpec(DbCommandKind.DataTable, "SELECT * FROM t WHERE a = {0}", "x");
            using var conn = new SqlConnection();

            var ex = Assert.Throws<InvalidOperationException>(() => spec.CreateCommand(DatabaseType.SQLServer, conn, "c001"));
            Assert.Contains(CommandTextVariable.CompanyId, ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An unbound command that references the company placeholder is refused")]
        public void CreateCommand_UnboundWithPlaceholder_Throws()
        {
            var spec = new DbCommandSpec(DbCommandKind.DataTable, CompanySql, "x");
            using var conn = new SqlConnection();

            Assert.Throws<InvalidOperationException>(() => spec.CreateCommand(DatabaseType.SQLServer, conn));
        }

        [Fact]
        [DisplayName("BindCompany stores the company in its lowercase form")]
        public void BindCompany_MixedCase_StoresLowercase()
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company"));

            dbAccess.BindCompany("C001");

            Assert.Equal("c001", dbAccess.BoundCompanyId);
        }

        [Fact]
        [DisplayName("BindCompany refuses to rebind a database access to another company")]
        public void BindCompany_AnotherCompany_Throws()
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company")).BindCompany("c001");

            Assert.Throws<InvalidOperationException>(() => dbAccess.BindCompany("c002"));
            Assert.Same(dbAccess, dbAccess.BindCompany("C001"));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a bound database access refuses a statement that forgot the company before it reaches the database")]
        public void Execute_BoundWithoutPlaceholder_Throws()
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company")).BindCompany("c001");

            Assert.Throws<InvalidOperationException>(() =>
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar, "SELECT COUNT(*) FROM st_department")));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a bound database access runs a statement that names the company")]
        public void Execute_BoundWithPlaceholder_Runs()
        {
            var dbAccess = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.SQLite, "company")).BindCompany("c001");

            var result = dbAccess.Execute(new DbCommandSpec(DbCommandKind.Scalar,
                $"SELECT COUNT(*) FROM st_department WHERE sys_company_id = {CommandTextVariable.CompanyId}"));

            Assert.NotNull(result.Scalar);
        }
    }
}
