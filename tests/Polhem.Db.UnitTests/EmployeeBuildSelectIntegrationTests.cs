using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Data.Common;
using Polhem.Db.Dml;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Round-trip integration tests: run the SELECT statements produced from the Employee/Department FormSchemas
    /// against a real database, to verify that each dialect's SQL is accepted and that JOIN/Filter/Sort behave as the
    /// FormSchema mapping says. Each test seeds its own data (Supervisor → Department → Employee) and cleans up in
    /// finally.
    /// </summary>
    public class EmployeeBuildSelectIntegrationTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        private const string CategoryId = "company";

        public EmployeeBuildSelectIntegrationTests(SharedDbFixture fx) { _fx = fx; }

        private IDefineAccess Access => _fx.GetRequiredService<IDefineAccess>();

        private void RunChainedSupervisorSelect(DatabaseType dbType)
        {
            var employeeSchema = Access.GetFormSchema("Employee");
            var departmentSchema = Access.GetFormSchema("Department");
            var db = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, CategoryId));

            string runId = Guid.NewGuid().ToString("N")[..8];
            var supervisorRowId = Guid.NewGuid();
            var deptRowId = Guid.NewGuid();
            var employeeRowId = Guid.NewGuid();

            try
            {
                InsertEmployee(db, employeeSchema, dbType, supervisorRowId, $"S{runId}", "主管甲", Guid.Empty);
                InsertDepartment(db, departmentSchema, dbType, deptRowId, $"D{runId}", "工程部", supervisorRowId);
                InsertEmployee(db, employeeSchema, dbType, employeeRowId, $"E{runId}", "員工乙", deptRowId);

                var spec = new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .Build("Employee",
                        "sys_id,sys_name,ref_dept_id,ref_dept_name,ref_supervisor_id,ref_supervisor_name",
                        FilterCondition.Equal("sys_rowid", employeeRowId));

                var table = db.Execute(spec).Table!;

                Assert.Single(table.Rows);
                var row = table.Rows[0];
                Assert.Equal($"E{runId}", row["sys_id"]);
                Assert.Equal("員工乙", row["sys_name"]);
                // Single-level relation: dept_rowid → Department.
                Assert.Equal($"D{runId}", row["ref_dept_id"]);
                Assert.Equal("工程部", row["ref_dept_name"]);
                // Multi-level relation: dept_rowid → Department → manager_rowid → Employee.
                Assert.Equal($"S{runId}", row["ref_supervisor_id"]);
                Assert.Equal("主管甲", row["ref_supervisor_name"]);
            }
            finally
            {
                TryDelete(db, employeeSchema, dbType, "Employee", employeeRowId);
                TryDelete(db, departmentSchema, dbType, "Department", deptRowId);
                TryDelete(db, employeeSchema, dbType, "Employee", supervisorRowId);
            }
        }

        private void RunFilterByRefDeptIdSelect(DatabaseType dbType)
        {
            var employeeSchema = Access.GetFormSchema("Employee");
            var departmentSchema = Access.GetFormSchema("Department");
            var db = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, CategoryId));

            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptARowId = Guid.NewGuid();
            var deptBRowId = Guid.NewGuid();
            var empARowId = Guid.NewGuid();
            var empBRowId = Guid.NewGuid();

            try
            {
                InsertDepartment(db, departmentSchema, dbType, deptARowId, $"DA{runId}", "甲部", Guid.Empty);
                InsertDepartment(db, departmentSchema, dbType, deptBRowId, $"DB{runId}", "乙部", Guid.Empty);
                InsertEmployee(db, employeeSchema, dbType, empARowId, $"EA{runId}", "員工A", deptARowId);
                InsertEmployee(db, employeeSchema, dbType, empBRowId, $"EB{runId}", "員工B", deptBRowId);

                // Filter by `ref_dept_id`, which requires the JOIN to `st_department`.
                var spec = new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .Build("Employee",
                        "sys_id,sys_name,ref_dept_id",
                        FilterCondition.Equal("ref_dept_id", $"DA{runId}"));

                var table = db.Execute(spec).Table!;

                Assert.Single(table.Rows);
                Assert.Equal($"EA{runId}", table.Rows[0]["sys_id"]);
                Assert.Equal($"DA{runId}", table.Rows[0]["ref_dept_id"]);
            }
            finally
            {
                TryDelete(db, employeeSchema, dbType, "Employee", empARowId);
                TryDelete(db, employeeSchema, dbType, "Employee", empBRowId);
                TryDelete(db, departmentSchema, dbType, "Department", deptARowId);
                TryDelete(db, departmentSchema, dbType, "Department", deptBRowId);
            }
        }

        private void RunSortByRefDeptNameSelect(DatabaseType dbType)
        {
            var employeeSchema = Access.GetFormSchema("Employee");
            var departmentSchema = Access.GetFormSchema("Department");
            var db = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, CategoryId));

            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptZRowId = Guid.NewGuid();
            var deptARowId = Guid.NewGuid();
            var empInZRowId = Guid.NewGuid();
            var empInARowId = Guid.NewGuid();

            try
            {
                // Department names are ASCII on purpose so the sort order is predictable: Z last, A first.
                InsertDepartment(db, departmentSchema, dbType, deptZRowId, $"DZ{runId}", "ZZZ", Guid.Empty);
                InsertDepartment(db, departmentSchema, dbType, deptARowId, $"DA{runId}", "AAA", Guid.Empty);
                InsertEmployee(db, employeeSchema, dbType, empInZRowId, $"EZ{runId}", "員工Z", deptZRowId);
                InsertEmployee(db, employeeSchema, dbType, empInARowId, $"EA{runId}", "員工A", deptARowId);

                // Sort by `ref_dept_name` ascending (requires the JOIN to `st_department`). The filter narrows the
                // rows so leftovers from other tests cannot interfere.
                var filter = FilterGroup.Any(
                    FilterCondition.Equal("sys_rowid", empInZRowId),
                    FilterCondition.Equal("sys_rowid", empInARowId)
                );
                var sort = new SortFieldCollection { new SortField("ref_dept_name", SortDirection.Asc) };

                var spec = new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .Build("Employee", "sys_id,sys_name,ref_dept_name", filter, sort);

                var table = db.Execute(spec).Table!;

                Assert.Equal(2, table.Rows.Count);
                Assert.Equal("AAA", table.Rows[0]["ref_dept_name"]);
                Assert.Equal($"EA{runId}", table.Rows[0]["sys_id"]);
                Assert.Equal("ZZZ", table.Rows[1]["ref_dept_name"]);
                Assert.Equal($"EZ{runId}", table.Rows[1]["sys_id"]);
            }
            finally
            {
                TryDelete(db, employeeSchema, dbType, "Employee", empInZRowId);
                TryDelete(db, employeeSchema, dbType, "Employee", empInARowId);
                TryDelete(db, departmentSchema, dbType, "Department", deptZRowId);
                TryDelete(db, departmentSchema, dbType, "Department", deptARowId);
            }
        }

        private void RunPagingSelect(DatabaseType dbType)
        {
            var employeeSchema = Access.GetFormSchema("Employee");
            var db = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, CategoryId));

            string runId = Guid.NewGuid().ToString("N")[..8];
            var rowIds = new Guid[5];
            for (int i = 0; i < 5; i++) { rowIds[i] = Guid.NewGuid(); }

            try
            {
                // sys_id "P{runId}-0".."P{runId}-4" gives deterministic ORDER BY sys_id ASC
                for (int i = 0; i < 5; i++)
                {
                    InsertEmployee(db, employeeSchema, dbType, rowIds[i], $"P{runId}-{i}", $"員工{i}", Guid.Empty);
                }

                var filter = FilterCondition.StartsWith("sys_id", $"P{runId}-");
                var sort = new SortFieldCollection { new SortField("sys_id", SortDirection.Asc) };

                // Page 1: skip=0 take=2 → rows 0,1
                var page0 = db.Execute(new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .Build("Employee", "sys_id,sys_name", filter, sort, skip: 0, take: 2)).Table!;
                Assert.Equal(2, page0.Rows.Count);
                Assert.Equal($"P{runId}-0", page0.Rows[0]["sys_id"]);
                Assert.Equal($"P{runId}-1", page0.Rows[1]["sys_id"]);

                // Page 2: skip=2 take=2 → rows 2,3
                var page1 = db.Execute(new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .Build("Employee", "sys_id,sys_name", filter, sort, skip: 2, take: 2)).Table!;
                Assert.Equal(2, page1.Rows.Count);
                Assert.Equal($"P{runId}-2", page1.Rows[0]["sys_id"]);
                Assert.Equal($"P{runId}-3", page1.Rows[1]["sys_id"]);

                // Page 3: skip=4 take=2 → row 4 only (partial last page)
                var page2 = db.Execute(new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .Build("Employee", "sys_id,sys_name", filter, sort, skip: 4, take: 2)).Table!;
                Assert.Single(page2.Rows);
                Assert.Equal($"P{runId}-4", page2.Rows[0]["sys_id"]);
            }
            finally
            {
                foreach (var id in rowIds)
                {
                    TryDelete(db, employeeSchema, dbType, "Employee", id);
                }
            }
        }

        private void RunBuildCount(DatabaseType dbType)
        {
            var employeeSchema = Access.GetFormSchema("Employee");
            var db = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(dbType, CategoryId));

            string runId = Guid.NewGuid().ToString("N")[..8];
            var rowIds = new Guid[5];
            for (int i = 0; i < 5; i++) { rowIds[i] = Guid.NewGuid(); }

            try
            {
                // 3 employees with "C{runId}-A*" prefix and 2 with "C{runId}-B*" prefix.
                for (int i = 0; i < 3; i++)
                {
                    InsertEmployee(db, employeeSchema, dbType, rowIds[i], $"C{runId}-A{i}", $"員工A{i}", Guid.Empty);
                }
                for (int i = 0; i < 2; i++)
                {
                    InsertEmployee(db, employeeSchema, dbType, rowIds[3 + i], $"C{runId}-B{i}", $"員工B{i}", Guid.Empty);
                }

                // Filter matches all 5
                var resultAll = db.Execute(new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .BuildCount("Employee", FilterCondition.StartsWith("sys_id", $"C{runId}-"))).Scalar;
                Assert.Equal(5, Convert.ToInt32(resultAll, CultureInfo.InvariantCulture));

                // Filter matches subset of 3 (A* rows)
                var resultSubset = db.Execute(new SelectCommandBuilder(employeeSchema, dbType, Access)
                    .BuildCount("Employee", FilterCondition.StartsWith("sys_id", $"C{runId}-A"))).Scalar;
                Assert.Equal(3, Convert.ToInt32(resultSubset, CultureInfo.InvariantCulture));
            }
            finally
            {
                foreach (var id in rowIds)
                {
                    TryDelete(db, employeeSchema, dbType, "Employee", id);
                }
            }
        }

        private static void InsertEmployee(DbAccess db, FormSchema schema, DatabaseType dbType,
            Guid rowId, string sysId, string sysName, Guid deptRowId)
        {
            var dt = new DataTable();
            dt.Columns.Add("sys_rowid", typeof(Guid));
            dt.Columns.Add("sys_id", typeof(string));
            dt.Columns.Add("sys_name", typeof(string));
            dt.Columns.Add("dept_rowid", typeof(Guid));
            var row = dt.NewRow();
            row["sys_rowid"] = rowId;
            row["sys_id"] = sysId;
            row["sys_name"] = sysName;
            row["dept_rowid"] = deptRowId;
            var spec = new InsertCommandBuilder(schema, dbType).Build("Employee", row);
            db.Execute(spec);
        }

        private static void InsertDepartment(DbAccess db, FormSchema schema, DatabaseType dbType,
            Guid rowId, string sysId, string sysName, Guid managerRowId)
        {
            var dt = new DataTable();
            dt.Columns.Add("sys_rowid", typeof(Guid));
            dt.Columns.Add("sys_id", typeof(string));
            dt.Columns.Add("sys_name", typeof(string));
            dt.Columns.Add("manager_rowid", typeof(Guid));
            var row = dt.NewRow();
            row["sys_rowid"] = rowId;
            row["sys_id"] = sysId;
            row["sys_name"] = sysName;
            row["manager_rowid"] = managerRowId;
            var spec = new InsertCommandBuilder(schema, dbType).Build("Department", row);
            db.Execute(spec);
        }

        private static void TryDelete(DbAccess db, FormSchema schema, DatabaseType dbType, string tableName, Guid rowId)
        {
            try
            {
                var spec = new DeleteCommandBuilder(schema, dbType)
                    .Build(tableName, FilterCondition.Equal("sys_rowid", rowId));
                db.Execute(spec);
            }
            catch (DbException ex)
            {
                // Cleanup is best-effort: a failed seed INSERT may leave no row to delete.
                // Do not mask the assertion failure message.
                Console.WriteLine($"EmployeeBuildSelectIntegrationTests: cleanup of {tableName}#{rowId} failed — {ex.GetType().Name}: {ex.Message}");
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server SELECT built from the Employee FormSchema runs and returns correct single-level and multi-level relation fields")]
        public void ChainedSupervisorSelect_SqlServer()
            => RunChainedSupervisorSelect(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server SELECT filtered by ref_dept_id runs and returns only the employees of that department")]
        public void FilterByRefDeptIdSelect_SqlServer()
            => RunFilterByRefDeptIdSelect(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server SELECT sorted by ref_dept_name runs and returns rows in ascending order of the relation field")]
        public void SortByRefDeptNameSelect_SqlServer()
            => RunSortByRefDeptNameSelect(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL SELECT built from the Employee FormSchema runs and returns correct single-level and multi-level relation fields")]
        public void ChainedSupervisorSelect_PostgreSql()
            => RunChainedSupervisorSelect(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL SELECT filtered by ref_dept_id runs and returns only the employees of that department")]
        public void FilterByRefDeptIdSelect_PostgreSql()
            => RunFilterByRefDeptIdSelect(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL SELECT sorted by ref_dept_name runs and returns rows in ascending order of the relation field")]
        public void SortByRefDeptNameSelect_PostgreSql()
            => RunSortByRefDeptNameSelect(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite SELECT built from the Employee FormSchema runs and returns correct single-level and multi-level relation fields")]
        public void ChainedSupervisorSelect_Sqlite()
            => RunChainedSupervisorSelect(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite SELECT filtered by ref_dept_id runs and returns only the employees of that department")]
        public void FilterByRefDeptIdSelect_Sqlite()
            => RunFilterByRefDeptIdSelect(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite SELECT sorted by ref_dept_name runs and returns rows in ascending order of the relation field")]
        public void SortByRefDeptNameSelect_Sqlite()
            => RunSortByRefDeptNameSelect(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SELECT built from the Employee FormSchema runs and returns correct single-level and multi-level relation fields")]
        public void ChainedSupervisorSelect_MySql()
            => RunChainedSupervisorSelect(DatabaseType.MySQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SELECT filtered by ref_dept_id runs and returns only the employees of that department")]
        public void FilterByRefDeptIdSelect_MySql()
            => RunFilterByRefDeptIdSelect(DatabaseType.MySQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL SELECT sorted by ref_dept_name runs and returns rows in ascending order of the relation field")]
        public void SortByRefDeptNameSelect_MySql()
            => RunSortByRefDeptNameSelect(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle SELECT built from the Employee FormSchema runs and returns correct single-level and multi-level relation fields")]
        public void ChainedSupervisorSelect_Oracle()
            => RunChainedSupervisorSelect(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle SELECT filtered by ref_dept_id runs and returns only the employees of that department")]
        public void FilterByRefDeptIdSelect_Oracle()
            => RunFilterByRefDeptIdSelect(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle SELECT sorted by ref_dept_name runs and returns rows in ascending order of the relation field")]
        public void SortByRefDeptNameSelect_Oracle()
            => RunSortByRefDeptNameSelect(DatabaseType.Oracle);

        // -------- Paged SELECT (skip / take) per dialect --------

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server paged SELECT returns the correct rows for three skip/take pages")]
        public void PagingSelect_SqlServer() => RunPagingSelect(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL paged SELECT returns the correct rows for three skip/take pages")]
        public void PagingSelect_PostgreSql() => RunPagingSelect(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite paged SELECT returns the correct rows for three skip/take pages")]
        public void PagingSelect_Sqlite() => RunPagingSelect(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL paged SELECT returns the correct rows for three skip/take pages")]
        public void PagingSelect_MySql() => RunPagingSelect(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle paged SELECT returns the correct rows for three skip/take pages")]
        public void PagingSelect_Oracle() => RunPagingSelect(DatabaseType.Oracle);

        // -------- BuildCount per dialect --------

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server BuildCount with a filter returns 5 for the full set and 3 for the subset")]
        public void BuildCount_SqlServer() => RunBuildCount(DatabaseType.SQLServer);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL BuildCount with a filter returns 5 for the full set and 3 for the subset")]
        public void BuildCount_PostgreSql() => RunBuildCount(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite BuildCount with a filter returns 5 for the full set and 3 for the subset")]
        public void BuildCount_Sqlite() => RunBuildCount(DatabaseType.SQLite);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL BuildCount with a filter returns 5 for the full set and 3 for the subset")]
        public void BuildCount_MySql() => RunBuildCount(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle BuildCount with a filter returns 5 for the full set and 3 for the subset")]
        public void BuildCount_Oracle() => RunBuildCount(DatabaseType.Oracle);

        // -------- MySQL UINT64_MAX sentinel: skip without take --------

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL skip without take uses the UINT64_MAX sentinel literal and returns every row after the OFFSET")]
        public void PagingSelect_MySql_SkipWithoutTake()
        {
            var employeeSchema = Access.GetFormSchema("Employee");
            var db = _fx.NewDbAccess(TestDbConventions.GetDatabaseId(DatabaseType.MySQL, CategoryId));

            string runId = Guid.NewGuid().ToString("N")[..8];
            var rowIds = new Guid[5];
            for (int i = 0; i < 5; i++) { rowIds[i] = Guid.NewGuid(); }

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    InsertEmployee(db, employeeSchema, DatabaseType.MySQL, rowIds[i], $"M{runId}-{i}", $"員工{i}", Guid.Empty);
                }

                var filter = FilterCondition.StartsWith("sys_id", $"M{runId}-");
                var sort = new SortFieldCollection { new SortField("sys_id", SortDirection.Asc) };

                // skip=2 take=null emits `LIMIT 18446744073709551615 OFFSET 2`; MySQL must accept it.
                var table = db.Execute(new SelectCommandBuilder(employeeSchema, DatabaseType.MySQL, Access)
                    .Build("Employee", "sys_id", filter, sort, skip: 2, take: null)).Table!;
                Assert.Equal(3, table.Rows.Count);
                Assert.Equal($"M{runId}-2", table.Rows[0]["sys_id"]);
                Assert.Equal($"M{runId}-3", table.Rows[1]["sys_id"]);
                Assert.Equal($"M{runId}-4", table.Rows[2]["sys_id"]);
            }
            finally
            {
                foreach (var id in rowIds)
                {
                    TryDelete(db, employeeSchema, DatabaseType.MySQL, "Employee", id);
                }
            }
        }
    }
}
