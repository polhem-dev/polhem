using System.ComponentModel;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Repository.System;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Round-trip tests of EmployeeRepository across the database providers: insert st_employee into the company DB
    /// (with the user_rowid / dept_rowid links), then verify that GetByUserRowId returns the matching employee and
    /// null for an unknown user.
    /// </summary>
    public class EmployeeRepositoryTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        public EmployeeRepositoryTests(SharedDbFixture fx) { _fx = fx; }

        private EmployeeRepository CreateRepo()
            => new EmployeeRepository(TestRepositoryContext.Create(_fx.GetRequiredService<IDbConnectionManager>()), Guid.Empty, string.Empty);

        private void RunRoundTrip(DatabaseType dbType)
        {
            var databaseId = TestDbConventions.GetDatabaseId(dbType, "company");
            var dbAccess = _fx.NewDbAccess(databaseId);

            var empRowId = Guid.NewGuid();
            var userRowId = Guid.NewGuid();
            var deptRowId = Guid.NewGuid();
            var empId = string.Concat("EMP_", Guid.NewGuid().ToString("N").AsSpan(0, 6));

            string tbl = dbType.QuoteIdentifier("st_employee");
            string colRowId = dbType.QuoteIdentifier("sys_rowid");
            string colId = dbType.QuoteIdentifier("sys_id");
            string colName = dbType.QuoteIdentifier("sys_name");
            string colDept = dbType.QuoteIdentifier("dept_rowid");
            string colUser = dbType.QuoteIdentifier("user_rowid");
            string cols = $"({colRowId}, {colId}, {colName}, {colDept}, {colUser})";

            dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                $"INSERT INTO {tbl} {cols} VALUES ({{0}}, {{1}}, {{2}}, {{3}}, {{4}})",
                empRowId, empId, "測試員工", deptRowId, userRowId));

            try
            {
                var employee = CreateRepo().GetByUserRowId(databaseId, userRowId);

                Assert.NotNull(employee);
                Assert.Equal(empRowId, employee!.RowId);
                Assert.Equal(empId, employee.EmployeeId);
                Assert.Equal(deptRowId, employee.DeptRowId);
                Assert.Equal(userRowId, employee.UserRowId);

                Assert.Null(CreateRepo().GetByUserRowId(databaseId, Guid.NewGuid()));
            }
            finally
            {
                dbAccess.Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                    $"DELETE FROM {tbl} WHERE {colRowId} = {{0}}", empRowId));
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("Employee round-trip on SQL Server")]
        public void RoundTrip_SqlServer() => RunRoundTrip(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("Employee round-trip on PostgreSQL")]
        public void RoundTrip_PostgreSql() => RunRoundTrip(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("Employee round-trip on SQLite")]
        public void RoundTrip_Sqlite() => RunRoundTrip(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("Employee round-trip on MySQL")]
        public void RoundTrip_MySql() => RunRoundTrip(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Employee round-trip on Oracle")]
        public void RoundTrip_Oracle() => RunRoundTrip(DatabaseType.Oracle);
    }
}
