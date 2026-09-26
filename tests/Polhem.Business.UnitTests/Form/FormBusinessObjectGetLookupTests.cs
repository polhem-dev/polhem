using System.ComponentModel;
using System.Data;
using Polhem.Business.Form;
using Polhem.Db;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// Round-trip integration tests that call <see cref="FormBusinessObject.GetLookup"/> to verify, against a real DB,
    /// the server-side resolution of the lookup field set (Employee declares no LookupFields → the default
    /// <c>sys_rowid,sys_id,sys_name</c>), SearchText filtering, default paging, and the
    /// <c>GetLookupFilter</c> business filter override point.
    /// </summary>
    public class FormBusinessObjectGetLookupTests : IClassFixture<SharedDbFixture>
    {
        private readonly SharedDbFixture _fx;
        private const string CategoryId = "company";
        private const string ProgId = "Employee";

        public FormBusinessObjectGetLookupTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("GetLookup throws ArgumentNullException for null")]
        public void GetLookup_NullArgs_Throws()
        {
            var bo = new FormBusinessObject(TestPolhemContext.Create(_fx), Guid.NewGuid(), ProgId);
            Assert.Throws<ArgumentNullException>(() => bo.GetLookup(null!));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: the default GetLookup projection contains only sys_rowid/sys_id/sys_name and applies default paging")]
        public void GetLookup_Sqlite_DefaultProjectionAndPaging()
        {
            var ctx = new TestContext(_fx, DatabaseType.SQLite);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var emp1 = Guid.NewGuid();
            var emp2 = Guid.NewGuid();
            try
            {
                InsertEmployee(ctx, emp1, $"LK{runId}-1", "員工甲");
                InsertEmployee(ctx, emp2, $"LK{runId}-2", "員工乙");

                var result = ctx.CreateBo().GetLookup(new GetLookupArgs { SearchText = $"LK{runId}" });

                Assert.NotNull(result.Table);
                Assert.Equal(2, result.Table!.Rows.Count);
                // Server-resolved projection: exactly the default lookup field set.
                Assert.Equal(3, result.Table.Columns.Count);
                Assert.True(result.Table.Columns.Contains("sys_rowid"));
                Assert.True(result.Table.Columns.Contains("sys_id"));
                Assert.True(result.Table.Columns.Contains("sys_name"));
                // Omitted paging falls back to the server default page size.
                Assert.NotNull(result.Paging);
                Assert.Equal(100, result.Paging!.PageSize);
            }
            finally
            {
                TryDelete(ctx, emp1);
                TryDelete(ctx, emp2);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: GetLookup SearchText matches both sys_id and sys_name")]
        public void GetLookup_Sqlite_SearchTextMatchesIdOrName()
        {
            var ctx = new TestContext(_fx, DatabaseType.SQLite);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var emp1 = Guid.NewGuid();
            var emp2 = Guid.NewGuid();
            try
            {
                InsertEmployee(ctx, emp1, $"LK{runId}-1", $"甲{runId}");
                InsertEmployee(ctx, emp2, $"LK{runId}-2", "員工乙");

                // Matches emp1 by sys_name only — the id pattern is shared by both rows.
                var result = ctx.CreateBo().GetLookup(new GetLookupArgs { SearchText = $"甲{runId}" });

                Assert.NotNull(result.Table);
                Assert.Single(result.Table!.Rows);
                Assert.Equal($"LK{runId}-1", result.Table.Rows[0]["sys_id"]);
            }
            finally
            {
                TryDelete(ctx, emp1);
                TryDelete(ctx, emp2);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a GetLookupFilter override narrows the search result with AND")]
        public void GetLookup_Sqlite_BusinessFilterNarrowsResult()
        {
            var ctx = new TestContext(_fx, DatabaseType.SQLite);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var emp1 = Guid.NewGuid();
            var emp2 = Guid.NewGuid();
            try
            {
                InsertEmployee(ctx, emp1, $"LK{runId}-1", "員工甲");
                InsertEmployee(ctx, emp2, $"LK{runId}-2", "員工乙");

                var bo = ctx.CreateFilteredBo(FilterCondition.Equal("sys_rowid", emp2));
                var result = bo.GetLookup(new GetLookupArgs { SearchText = $"LK{runId}" });

                Assert.NotNull(result.Table);
                Assert.Single(result.Table!.Rows);
                Assert.Equal($"LK{runId}-2", result.Table.Rows[0]["sys_id"]);
            }
            finally
            {
                TryDelete(ctx, emp1);
                TryDelete(ctx, emp2);
            }
        }

        /// <summary>
        /// A test BO that uses a FilterNode injected through the constructor as the <see cref="FormBusinessObject.GetLookupFilter"/>
        /// business filter, to verify that the hook is combined with the search filter by AND.
        /// </summary>
        private sealed class FilteredLookupBo : FormBusinessObject
        {
            private readonly FilterNode _filter;

            public FilteredLookupBo(IPolhemContext ctx, Guid accessToken, string progId, FilterNode filter)
                : base(ctx, accessToken, progId)
            {
                _filter = filter;
            }

            protected override FilterNode? GetLookupFilter() => _filter;
        }

        private static void InsertEmployee(TestContext ctx, Guid rowId, string sysId, string sysName)
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
            row["dept_rowid"] = Guid.Empty;
            var spec = new InsertCommandBuilder(ctx.EmployeeSchema, ctx.DbType).Build("Employee", row);
            ctx.DbAccess.Execute(spec);
        }

        private static void TryDelete(TestContext ctx, Guid rowId)
        {
            try
            {
                var spec = new DeleteCommandBuilder(ctx.EmployeeSchema, ctx.DbType)
                    .Build("Employee", FilterCondition.Equal("sys_rowid", rowId));
                ctx.DbAccess.Execute(spec);
            }
            catch (Exception ex)
            {
                // Cleanup is best-effort: the row may not exist if the seed INSERT failed, and this must not mask the assertion failure message.
                Console.WriteLine($"FormBusinessObjectGetLookupTests: cleanup of Employee#{rowId} failed — {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Per-test wiring: binds <see cref="FormBusinessObject"/> to a <see cref="DataFormRepository"/> built with the
        /// test-specific <c>{categoryId}_{dbtype}</c> databaseId.
        /// </summary>
        private sealed class TestContext
        {
            private readonly SharedDbFixture _fx;
            private readonly IDataFormRepository _repository;

            public TestContext(SharedDbFixture fx, DatabaseType dbType)
            {
                _fx = fx;
                DbType = dbType;
                var databaseId = TestDbConventions.GetDatabaseId(dbType, CategoryId);
                DbAccess = fx.NewDbAccess(databaseId);

                var defineAccess = fx.GetRequiredService<IDefineAccess>();
                EmployeeSchema = defineAccess.GetFormSchema("Employee");

                _repository = new DataFormRepository(TestRepositoryContext.Create(fx.GetRequiredService<IDbConnectionManager>(), defineAccess: defineAccess, dbAccessFactory: fx.GetRequiredService<IDbAccessFactory>()), ProgId, EmployeeSchema, databaseId);
            }

            public DatabaseType DbType { get; }
            public DbAccess DbAccess { get; }
            public FormSchema EmployeeSchema { get; }

            public FormBusinessObject CreateBo()
            {
                var ctx = CreateContext();
                return new FormBusinessObject(ctx, Guid.NewGuid(), ProgId);
            }

            public FilteredLookupBo CreateFilteredBo(FilterNode filter)
            {
                var ctx = CreateContext();
                return new FilteredLookupBo(ctx, Guid.NewGuid(), ProgId, filter);
            }

            private IPolhemContext CreateContext()
            {
                var factory = new StubFactory(_repository);
                return TestPolhemContext.CreateWithOverrides(_fx, (typeof(IRepositoryFactory), factory));
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
