using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Db;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// Round-trip integration tests that call <see cref="FormBusinessObject.GetList"/> to verify the chain
    /// BO → <see cref="DataFormRepository"/> → <c>IFormCommandBuilder</c> → real DB.
    /// Seed data and cleanup follow the <c>EmployeeBuildSelectIntegrationTests</c> pattern.
    /// </summary>
    public class FormBusinessObjectGetListTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        private const string CategoryId = "company";
        private const string ProgId = "Employee";

        public FormBusinessObjectGetListTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("GetList throws ArgumentNullException for null")]
        public void GetList_NullArgs_Throws()
        {
            var bo = new FormBusinessObject(TestBusinessObjectContext.Create(_fx), Guid.NewGuid(), ProgId);
            Assert.Throws<ArgumentNullException>(() => bo.GetList(null!));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetList with explicit SelectFields returns only those fields, including relation fields")]
        public void GetList_Sqlite_ExplicitSelectFields()
            => RunExplicitSelectFields(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetList with both Filter and Sort returns the expected row count and order")]
        public void GetList_Sqlite_FilterAndSort()
            => RunFilterAndSort(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetList with explicit SelectFields returns only those fields, including relation fields")]
        public void GetList_SqlServer_ExplicitSelectFields()
            => RunExplicitSelectFields(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetList with both Filter and Sort returns the expected row count and order")]
        public void GetList_SqlServer_FilterAndSort()
            => RunFilterAndSort(DatabaseType.SQLServer);

        // -------- Record-scope shaped filters (Phase 3) --------

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetList with dept_rowid IN (the scope shape) returns only that department's rows without a remap error")]
        public void GetList_Sqlite_InFilterOnDeptField() => RunInFilterOnDeptField(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: GetList with dept_rowid IN (the scope shape) returns only that department's rows without a remap error")]
        public void GetList_SqlServer_InFilterOnDeptField() => RunInFilterOnDeptField(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetData with a scope filter returns the data in scope and null out of scope")]
        public void GetData_Sqlite_ScopeFilter() => RunGetDataScope(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: Delete/ExistsInScope re-query authoritatively, deleting 0 out of scope and 1 in scope")]
        public void Delete_Sqlite_ScopeFilter() => RunDeleteScope(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: Delete/ExistsInScope re-query authoritatively, deleting 0 out of scope and 1 in scope")]
        public void Delete_SqlServer_ScopeFilter() => RunDeleteScope(DatabaseType.SQLServer);

        private void RunInFilterOnDeptField(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptA = Guid.NewGuid();
            var deptB = Guid.NewGuid();
            var empA = Guid.NewGuid();
            var empB = Guid.NewGuid();
            try
            {
                InsertDepartment(ctx, deptA, $"DA{runId}", "A部", Guid.Empty);
                InsertDepartment(ctx, deptB, $"DB{runId}", "B部", Guid.Empty);
                InsertEmployee(ctx, empA, $"EA{runId}", "員工A", deptA);
                InsertEmployee(ctx, empB, $"EB{runId}", "員工B", deptB);

                // The scope shape is `dept_rowid IN (deptA)`. `WhereBuilder` must remap the master table column `dept_rowid` correctly.
                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id,dept_rowid",
                    Filter = new FilterCondition
                    {
                        FieldName = "dept_rowid",
                        Operator = ComparisonOperator.In,
                        Value = new List<object> { deptA },
                    },
                });

                Assert.NotNull(result.Table);
                Assert.Single(result.Table!.Rows);  // Only empA, in department A.
                Assert.Equal($"EA{runId}", result.Table.Rows[0]["sys_id"]);
            }
            finally
            {
                TryDelete(ctx, "Employee", empA);
                TryDelete(ctx, "Employee", empB);
                TryDelete(ctx, "Department", deptA);
                TryDelete(ctx, "Department", deptB);
            }
        }

        private void RunGetDataScope(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptA = Guid.NewGuid();
            var empA = Guid.NewGuid();
            try
            {
                InsertDepartment(ctx, deptA, $"DA{runId}", "A部", Guid.Empty);
                InsertEmployee(ctx, empA, $"EA{runId}", "員工A", deptA);

                var inScope = new FilterCondition { FieldName = "dept_rowid", Operator = ComparisonOperator.In, Value = new List<object> { deptA } };
                var outScope = new FilterCondition { FieldName = "dept_rowid", Operator = ComparisonOperator.In, Value = new List<object> { Guid.NewGuid() } };

                Assert.NotNull(ctx.Repository.GetData(empA, inScope));   // In scope.
                Assert.Null(ctx.Repository.GetData(empA, outScope));     // Out of scope → null.
                Assert.NotNull(ctx.Repository.GetData(empA));            // No scope → returned as usual.
            }
            finally
            {
                TryDelete(ctx, "Employee", empA);
                TryDelete(ctx, "Department", deptA);
            }
        }

        private void RunDeleteScope(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptA = Guid.NewGuid();
            var empA = Guid.NewGuid();
            try
            {
                InsertDepartment(ctx, deptA, $"DA{runId}", "A部", Guid.Empty);
                InsertEmployee(ctx, empA, $"EA{runId}", "員工A", deptA);

                var inScope = new FilterCondition { FieldName = "dept_rowid", Operator = ComparisonOperator.In, Value = new List<object> { deptA } };
                var outScope = new FilterCondition { FieldName = "dept_rowid", Operator = ComparisonOperator.In, Value = new List<object> { Guid.NewGuid() } };

                Assert.True(ctx.Repository.ExistsInScope(empA, inScope));   // Exists in scope.
                Assert.False(ctx.Repository.ExistsInScope(empA, outScope)); // Out of scope counts as not existing.

                Assert.Equal(0, ctx.Repository.Delete(empA, outScope));     // Out of scope → 0 deleted, nothing removed.
                Assert.NotNull(ctx.Repository.GetData(empA));               // Still there.
                Assert.Equal(1, ctx.Repository.Delete(empA, inScope));      // In scope → 1 deleted.
                Assert.Null(ctx.Repository.GetData(empA));                  // Deleted.
            }
            finally
            {
                TryDelete(ctx, "Employee", empA);
                TryDelete(ctx, "Department", deptA);
            }
        }

        // -------- Paging --------

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetList with Paging=null returns every row that fits one page of MaxPageSize and reports the cap in Result.Paging")]
        public void GetList_Sqlite_PagingNull_ServedAsFirstCappedPage()
            => RunPagingNullBehavior(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: paged GetList with IncludeTotalCount returns the correct TotalCount/HasMore")]
        public void GetList_Sqlite_PagedWithTotalCount()
            => RunPagedWithTotalCount(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: paged GetList without IncludeTotalCount infers HasMore with a probe row and leaves TotalCount null")]
        public void GetList_Sqlite_PagedWithoutTotalCount()
            => RunPagedWithoutTotalCount(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: paged GetList with a Page beyond the last page returns an empty Table and HasMore=false")]
        public void GetList_Sqlite_PagedBeyondLastPage()
            => RunPagedBeyondLastPage(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetList with a PageSize above MaxPageSize clamps it to the cap without throwing")]
        public void GetList_Sqlite_PageSizeClampedToCap()
            => RunPageSizeClampedToCap(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: paged GetList with SortFields=null falls back to sys_no ASC")]
        public void GetList_Sqlite_SortFallbackToSysNo()
            => RunSortFallbackToSysNo(DatabaseType.SQLite);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: paged GetList with IncludeTotalCount returns the correct TotalCount/HasMore")]
        public void GetList_SqlServer_PagedWithTotalCount()
            => RunPagedWithTotalCount(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: paged GetList without IncludeTotalCount infers HasMore with a probe row and leaves TotalCount null")]
        public void GetList_SqlServer_PagedWithoutTotalCount()
            => RunPagedWithoutTotalCount(DatabaseType.SQLServer);

        // -------- Oracle --------
        // The FormSchema-driven query path had no Oracle coverage at all until 4.27.x; the
        // framework repositories were exercised on Oracle but this path never was, and it was
        // broken on Oracle the whole time. These mirror the SQL Server cases above.

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: GetList with explicit SelectFields returns only those fields, including relation fields")]
        public void GetList_Oracle_ExplicitSelectFields()
            => RunExplicitSelectFields(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: GetList with both Filter and Sort returns the expected row count and order")]
        public void GetList_Oracle_FilterAndSort()
            => RunFilterAndSort(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: GetList with dept_rowid IN (the scope shape) returns only that department's rows without a remap error")]
        public void GetList_Oracle_InFilterOnDeptField()
            => RunInFilterOnDeptField(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: paged GetList with IncludeTotalCount returns the correct TotalCount/HasMore")]
        public void GetList_Oracle_PagedWithTotalCount()
            => RunPagedWithTotalCount(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: paged GetList without IncludeTotalCount infers HasMore with a probe row and leaves TotalCount null")]
        public void GetList_Oracle_PagedWithoutTotalCount()
            => RunPagedWithoutTotalCount(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: paged GetList with SortFields=null falls back to sys_no ASC")]
        public void GetList_Oracle_SortFallbackToSysNo()
            => RunSortFallbackToSysNo(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a paged GetList without Filter or Sort (the load test query shape) succeeds")]
        public void GetList_Oracle_PagedWithoutFilter()
            => RunPagedWithoutFilter(DatabaseType.Oracle);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: the sys_rowid column returned by GetList is a Guid, not the byte[] of RAW(16)")]
        public void GetList_Oracle_RowIdColumnIsGuid()
        {
            var ctx = new TestContext(_fx, DatabaseType.Oracle);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var employeeRowId = Guid.NewGuid();
            try
            {
                InsertEmployee(ctx, employeeRowId, $"E{runId}", "員工甲", Guid.Empty);

                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_rowid,sys_id",
                    Filter = FilterCondition.Equal("sys_rowid", employeeRowId),
                });

                Assert.NotNull(result.Table);
                Assert.Equal(typeof(Guid), result.Table!.Columns["sys_rowid"]!.DataType);
                Assert.Equal(employeeRowId, result.Table.Rows[0]["sys_rowid"]);
            }
            finally
            {
                TryDelete(ctx, "Employee", employeeRowId);
            }
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a paged GetList without Filter or Sort (the load test query shape) succeeds")]
        public void GetList_SqlServer_PagedWithoutFilter()
            => RunPagedWithoutFilter(DatabaseType.SQLServer);

        private void RunExplicitSelectFields(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptRowId = Guid.NewGuid();
            var employeeRowId = Guid.NewGuid();

            try
            {
                InsertDepartment(ctx, deptRowId, $"D{runId}", "工程部", Guid.Empty);
                InsertEmployee(ctx, employeeRowId, $"E{runId}", "員工乙", deptRowId);

                var bo = ctx.CreateBo();
                var args = new GetListArgs
                {
                    SelectFields = "sys_id,sys_name,ref_dept_name",
                    Filter = FilterCondition.Equal("sys_rowid", employeeRowId)
                };
                var result = bo.GetList(args);

                Assert.NotNull(result.Table);
                Assert.Single(result.Table!.Rows);
                var row = result.Table.Rows[0];
                Assert.Equal($"E{runId}", row["sys_id"]);
                Assert.Equal("員工乙", row["sys_name"]);
                Assert.Equal("工程部", row["ref_dept_name"]);
                // Fields that were not requested must not be present.
                Assert.False(result.Table.Columns.Contains("ref_supervisor_name"));
            }
            finally
            {
                TryDelete(ctx, "Employee", employeeRowId);
                TryDelete(ctx, "Department", deptRowId);
            }
        }

        private void RunFilterAndSort(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptZRowId = Guid.NewGuid();
            var deptARowId = Guid.NewGuid();
            var empInZRowId = Guid.NewGuid();
            var empInARowId = Guid.NewGuid();

            try
            {
                InsertDepartment(ctx, deptZRowId, $"DZ{runId}", "ZZZ", Guid.Empty);
                InsertDepartment(ctx, deptARowId, $"DA{runId}", "AAA", Guid.Empty);
                InsertEmployee(ctx, empInZRowId, $"EZ{runId}", "員工Z", deptZRowId);
                InsertEmployee(ctx, empInARowId, $"EA{runId}", "員工A", deptARowId);

                var bo = ctx.CreateBo();
                var args = new GetListArgs
                {
                    SelectFields = "sys_id,ref_dept_name",
                    Filter = FilterGroup.Any(
                        FilterCondition.Equal("sys_rowid", empInZRowId),
                        FilterCondition.Equal("sys_rowid", empInARowId)),
                    SortFields = [new SortField("ref_dept_name", SortDirection.Asc)]
                };
                var result = bo.GetList(args);

                Assert.NotNull(result.Table);
                Assert.Equal(2, result.Table!.Rows.Count);
                Assert.Equal("AAA", result.Table.Rows[0]["ref_dept_name"]);
                Assert.Equal($"EA{runId}", result.Table.Rows[0]["sys_id"]);
                Assert.Equal("ZZZ", result.Table.Rows[1]["ref_dept_name"]);
                Assert.Equal($"EZ{runId}", result.Table.Rows[1]["sys_id"]);
            }
            finally
            {
                TryDelete(ctx, "Employee", empInZRowId);
                TryDelete(ctx, "Employee", empInARowId);
                TryDelete(ctx, "Department", deptZRowId);
                TryDelete(ctx, "Department", deptARowId);
            }
        }

        // Seeds 5 employees with sys_id "P{runId}-0" .. "P{runId}-4" and returns the
        // rowIds plus a StartsWith filter scoped to this run. Caller is responsible
        // for cleanup via TryDelete.
        private static (Guid[] rowIds, FilterNode filter, string runId) SeedFivePagingRows(TestContext ctx)
        {
            string runId = Guid.NewGuid().ToString("N")[..8];
            var rowIds = new Guid[5];
            for (int i = 0; i < 5; i++)
            {
                rowIds[i] = Guid.NewGuid();
                InsertEmployee(ctx, rowIds[i], $"P{runId}-{i}", $"員工{i}", Guid.Empty);
            }
            return (rowIds, FilterCondition.StartsWith("sys_id", $"P{runId}-"), runId);
        }

        private void RunPagingNullBehavior(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            var (rowIds, filter, _) = SeedFivePagingRows(ctx);
            try
            {
                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id",
                    Filter = filter,
                });

                Assert.NotNull(result.Table);
                Assert.Equal(5, result.Table!.Rows.Count);
                // A request without paging is served as the first page of the framework cap, so a client
                // can never read a whole table in one call; the paging metadata says nothing was left out.
                Assert.NotNull(result.Paging);
                Assert.Equal(1, result.Paging!.Page);
                Assert.Equal(PagingOptions.MaxPageSize, result.Paging.PageSize);
                Assert.False(result.Paging.HasMore);
            }
            finally
            {
                foreach (var id in rowIds) TryDelete(ctx, "Employee", id);
            }
        }

        private void RunPagedWithTotalCount(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            var (rowIds, filter, runId) = SeedFivePagingRows(ctx);
            try
            {
                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id",
                    Filter = filter,
                    SortFields = [new SortField("sys_id", SortDirection.Asc)],
                    Paging = new PagingOptions { Page = 2, PageSize = 2, IncludeTotalCount = true },
                });

                Assert.NotNull(result.Table);
                Assert.Equal(2, result.Table!.Rows.Count);
                Assert.Equal($"P{runId}-2", result.Table.Rows[0]["sys_id"]);
                Assert.Equal($"P{runId}-3", result.Table.Rows[1]["sys_id"]);

                Assert.NotNull(result.Paging);
                Assert.Equal(2, result.Paging!.Page);
                Assert.Equal(2, result.Paging.PageSize);
                Assert.Equal(5, result.Paging.TotalCount);
                Assert.True(result.Paging.HasMore);  // 5 rows: page 2 takes 2 rows and 1 row remains.
            }
            finally
            {
                foreach (var id in rowIds) TryDelete(ctx, "Employee", id);
            }
        }

        private void RunPagedWithoutTotalCount(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            var (rowIds, filter, runId) = SeedFivePagingRows(ctx);
            try
            {
                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id",
                    Filter = filter,
                    SortFields = [new SortField("sys_id", SortDirection.Asc)],
                    Paging = new PagingOptions { Page = 1, PageSize = 2, IncludeTotalCount = false },
                });

                Assert.NotNull(result.Table);
                // The probe row has been trimmed, so the page keeps only 2 rows.
                Assert.Equal(2, result.Table!.Rows.Count);
                Assert.Equal($"P{runId}-0", result.Table.Rows[0]["sys_id"]);
                Assert.Equal($"P{runId}-1", result.Table.Rows[1]["sys_id"]);

                Assert.NotNull(result.Paging);
                Assert.Null(result.Paging!.TotalCount);  // Not requested.
                Assert.True(result.Paging.HasMore);
            }
            finally
            {
                foreach (var id in rowIds) TryDelete(ctx, "Employee", id);
            }
        }

        private void RunPagedBeyondLastPage(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            var (rowIds, filter, _) = SeedFivePagingRows(ctx);
            try
            {
                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id",
                    Filter = filter,
                    SortFields = [new SortField("sys_id", SortDirection.Asc)],
                    Paging = new PagingOptions { Page = 99, PageSize = 2, IncludeTotalCount = true },
                });

                Assert.NotNull(result.Table);
                Assert.Empty(result.Table!.Rows);

                Assert.NotNull(result.Paging);
                Assert.Equal(5, result.Paging!.TotalCount);
                Assert.False(result.Paging.HasMore);
            }
            finally
            {
                foreach (var id in rowIds) TryDelete(ctx, "Employee", id);
            }
        }

        private void RunPageSizeClampedToCap(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            var (rowIds, filter, _) = SeedFivePagingRows(ctx);
            try
            {
                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id",
                    Filter = filter,
                    SortFields = [new SortField("sys_id", SortDirection.Asc)],
                    // int.MaxValue should be clamped to MaxPageSize (1000) without exceptions.
                    Paging = new PagingOptions { Page = 1, PageSize = int.MaxValue, IncludeTotalCount = false },
                });

                Assert.NotNull(result.Table);
                Assert.Equal(5, result.Table!.Rows.Count);  // 5 rows still fit in the clamped PageSize.
                Assert.NotNull(result.Paging);
                Assert.Equal(1000, result.Paging!.PageSize);  // The clamped value.
                Assert.False(result.Paging.HasMore);
            }
            finally
            {
                foreach (var id in rowIds) TryDelete(ctx, "Employee", id);
            }
        }

        private void RunSortFallbackToSysNo(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            var (rowIds, filter, runId) = SeedFivePagingRows(ctx);
            try
            {
                // With `SortFields` null, the repository falls back to `sys_no ASC`.
                // `sys_no` is AutoIncrement and increases in seed order. Page 1 takes 2 rows and page 2 takes the next 2,
                // and `sys_id` keeps its 0..4 order too, because both columns were inserted in the same order.
                var page1 = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id",
                    Filter = filter,
                    SortFields = null,
                    Paging = new PagingOptions { Page = 1, PageSize = 2, IncludeTotalCount = false },
                });

                Assert.Equal(2, page1.Table!.Rows.Count);
                Assert.Equal($"P{runId}-0", page1.Table.Rows[0]["sys_id"]);
                Assert.Equal($"P{runId}-1", page1.Table.Rows[1]["sys_id"]);

                var page2 = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id",
                    Filter = filter,
                    SortFields = null,
                    Paging = new PagingOptions { Page = 2, PageSize = 2, IncludeTotalCount = false },
                });

                Assert.Equal(2, page2.Table!.Rows.Count);
                Assert.Equal($"P{runId}-2", page2.Table.Rows[0]["sys_id"]);
                Assert.Equal($"P{runId}-3", page2.Table.Rows[1]["sys_id"]);
            }
            finally
            {
                foreach (var id in rowIds) TryDelete(ctx, "Employee", id);
            }
        }

        // The load-test shape: paging with neither a filter nor an explicit sort, so the SELECT
        // carries no bind variables at all and the Repository supplies the `sys_no` fallback sort.
        private void RunPagedWithoutFilter(DatabaseType dbType)
        {
            var ctx = new TestContext(_fx, dbType);
            var (rowIds, _, _) = SeedFivePagingRows(ctx);
            try
            {
                var result = ctx.CreateBo().GetList(new GetListArgs
                {
                    SelectFields = "sys_id,sys_name",
                    Paging = new PagingOptions { Page = 1, PageSize = 3, IncludeTotalCount = true },
                });

                Assert.NotNull(result.Table);
                // Rows are whatever the shared table holds, so only the page shape is asserted.
                Assert.True(result.Table!.Rows.Count <= 3);
                Assert.NotNull(result.Paging);
                Assert.True(result.Paging!.TotalCount >= 5);
            }
            finally
            {
                foreach (var id in rowIds) TryDelete(ctx, "Employee", id);
            }
        }

        private static void InsertEmployee(TestContext ctx, Guid rowId, string sysId, string sysName, Guid deptRowId)
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
            var spec = new InsertCommandBuilder(ctx.EmployeeSchema, ctx.DbType).Build("Employee", row);
            ctx.DbAccess.Execute(spec);
        }

        private static void InsertDepartment(TestContext ctx, Guid rowId, string sysId, string sysName, Guid managerRowId)
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
            var spec = new InsertCommandBuilder(ctx.DepartmentSchema, ctx.DbType).Build("Department", row);
            ctx.DbAccess.Execute(spec);
        }

        private static void TryDelete(TestContext ctx, string tableName, Guid rowId)
        {
            try
            {
                var schema = tableName == "Employee" ? ctx.EmployeeSchema : ctx.DepartmentSchema;
                var spec = new DeleteCommandBuilder(schema, ctx.DbType)
                    .Build(tableName, FilterCondition.Equal("sys_rowid", rowId));
                ctx.DbAccess.Execute(spec);
            }
            catch (Exception ex)
            {
                // Cleanup is best-effort: the row may not exist if the seed INSERT failed, and this must not mask the assertion failure message.
                Console.WriteLine($"FormBusinessObjectGetListTests: cleanup of {tableName}#{rowId} failed — {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Per-test wiring: binds <see cref="FormBusinessObject"/> to a <see cref="DataFormRepository"/>
        /// constructed against the test-specific <c>{categoryId}_{dbtype}</c> databaseId.
        /// The production <see cref="FormRepositoryFactory"/> uses <c>CategoryId</c> directly,
        /// which doesn't match the multi-DB-per-category test layout.
        /// </summary>
        private sealed class TestContext
        {
            private readonly SharedDbFixture _fx;
            private readonly string _databaseId;
            private readonly IDataFormRepository _repository;

            public TestContext(SharedDbFixture fx, DatabaseType dbType)
            {
                _fx = fx;
                DbType = dbType;
                _databaseId = TestDbConventions.GetDatabaseId(dbType, CategoryId);
                DbAccess = fx.NewDbAccess(_databaseId);

                var defineAccess = fx.GetRequiredService<IDefineAccess>();
                EmployeeSchema = defineAccess.GetFormSchema("Employee");
                DepartmentSchema = defineAccess.GetFormSchema("Department");

                _repository = new DataFormRepository(TestRepositoryContext.Create(fx.GetRequiredService<IDbConnectionManager>(), defineAccess: defineAccess, dbAccessFactory: fx.GetRequiredService<IDbAccessFactory>()), ProgId, EmployeeSchema, _databaseId);
            }

            public DatabaseType DbType { get; }
            public DbAccess DbAccess { get; }
            public FormSchema EmployeeSchema { get; }
            public FormSchema DepartmentSchema { get; }
            public IDataFormRepository Repository => _repository;

            public FormBusinessObject CreateBo()
            {
                var factory = new StubFactory(_repository);
                var ctx = TestBusinessObjectContext.CreateWithOverrides(_fx, (typeof(IRepositoryFactory), factory));
                return new FormBusinessObject(ctx, Guid.NewGuid(), ProgId);
            }
        }

        private sealed class StubFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _repository;
            public StubFactory(IDataFormRepository repository) => _repository = repository;
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_repository;
            public T Create<T>(Guid accessToken = default) where T : class
                => throw new NotSupportedException();
        }
    }
}
