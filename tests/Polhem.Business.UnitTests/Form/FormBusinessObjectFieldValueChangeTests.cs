using System.ComponentModel;
using System.Data;
using System.Data.Common;
using Polhem.Business.Form;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Db;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// <see cref="FormBusinessObject.FieldValueChange"/>: a test-built form whose <c>emp_rowid</c> relates to the
    /// fixture's Employee form fills its mapping destinations from the Employee row, including a value Employee itself
    /// reads through its own relation to Department.
    /// </summary>
    public class FormBusinessObjectFieldValueChangeTests : IClassFixture<SharedDbFixture>
    {
        private const string ProgId = "FvcCustomer";
        private const string CategoryId = "company";
        private readonly SharedDbFixture _fx;

        public FormBusinessObjectFieldValueChangeTests(SharedDbFixture fx) { _fx = fx; }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a picked employee fills every mapping destination, including the department code read through Employee's own relation")]
        public void FieldValueChange_Sqlite_ValidRow_FillsDestinations()
        {
            var wiring = new Wiring(_fx, DatabaseType.SQLite);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var deptId = Guid.NewGuid();
            var empId = Guid.NewGuid();
            try
            {
                InsertDepartment(wiring, deptId, $"D{runId}", "Engineering");
                InsertEmployee(wiring, empId, $"E{runId}", "Alice", deptId);

                var result = wiring.CreateBo().FieldValueChange(Args(empId.ToString()));

                Assert.Empty(result.Errors);
                var values = Assert.Single(result.Values!.Rows.Cast<DataRow>());
                Assert.Equal($"E{runId}", values["ref_emp_id"]);
                Assert.Equal("Alice", values["ref_emp_name"]);
                Assert.Equal(deptId, ValueUtilities.CGuid(values["dept_rowid"]));
                Assert.Equal($"D{runId}", values["ref_emp_dept_id"]);
            }
            finally
            {
                TryDelete(wiring, wiring.EmployeeSchema, "Employee", empId);
                TryDelete(wiring, wiring.DepartmentSchema, "Department", deptId);
            }
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a value that names no employee clears every destination and reports an error on the field")]
        public void FieldValueChange_Sqlite_UnknownRow_ClearsAndReportsError()
        {
            var wiring = new Wiring(_fx, DatabaseType.SQLite);

            var result = wiring.CreateBo().FieldValueChange(Args(Guid.NewGuid().ToString()));

            Assert.True(result.Errors.ContainsKey("emp_rowid"));
            AssertCleared(result);
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a row the related form's lookup filter excludes cannot be resolved")]
        public void ResolveRelationRow_Sqlite_LookupFilterExcludes_ReturnsNull()
        {
            var wiring = new Wiring(_fx, DatabaseType.SQLite);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var empId = Guid.NewGuid();
            try
            {
                InsertEmployee(wiring, empId, $"E{runId}", "Bob", Guid.Empty);

                var open = new FormBusinessObject(wiring.Context, wiring.Token, "Employee");
                var filtered = new ExcludingLookupBo(wiring.Context, wiring.Token, "Employee",
                    FilterCondition.NotEqual("sys_rowid", empId));

                Assert.NotNull(open.ResolveRelationRow(empId, ["sys_id"]));
                Assert.Null(filtered.ResolveRelationRow(empId, ["sys_id"]));
            }
            finally
            {
                TryDelete(wiring, wiring.EmployeeSchema, "Employee", empId);
            }
        }

        [Fact]
        [DisplayName("A cleared relation value gives every destination its type's empty value, not null, without an error")]
        public void FieldValueChange_EmptyValue_ClearsDestinations()
        {
            var result = new Wiring(_fx, DatabaseType.SQLite).CreateBo().FieldValueChange(Args(string.Empty));

            Assert.Empty(result.Errors);
            AssertCleared(result);
        }

        [Fact]
        [DisplayName("A row table of more than one row is refused")]
        public void FieldValueChange_SeveralRows_Throws()
        {
            var rows = new DataTable(ProgId);
            rows.Columns.Add("emp_rowid", typeof(Guid));
            rows.Rows.Add(Guid.NewGuid());
            rows.Rows.Add(Guid.NewGuid());
            var args = Args(string.Empty);
            args.Row = rows;

            Assert.Throws<ArgumentException>(() => new Wiring(_fx, DatabaseType.SQLite).CreateBo().FieldValueChange(args));
        }

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a mapping whose source the related form does not have leaves its destination out of the result")]
        public void FieldValueChange_Sqlite_MissingSource_LeavesDestinationOut()
        {
            var wiring = new Wiring(_fx, DatabaseType.SQLite, missingSource: true);
            string runId = Guid.NewGuid().ToString("N")[..8];
            var empId = Guid.NewGuid();
            try
            {
                InsertEmployee(wiring, empId, $"E{runId}", "Carol", Guid.Empty);

                var result = wiring.CreateBo().FieldValueChange(Args(empId.ToString()));

                Assert.Empty(result.Errors);
                Assert.Equal($"E{runId}", result.Values!.Rows[0]["ref_emp_id"]);
                Assert.False(result.Values.Columns.Contains("ref_emp_title"));
            }
            finally
            {
                TryDelete(wiring, wiring.EmployeeSchema, "Employee", empId);
            }
        }

        private static void AssertCleared(FieldValueChangeResult result)
        {
            var values = Assert.Single(result.Values!.Rows.Cast<DataRow>());
            Assert.Equal(string.Empty, values["ref_emp_id"]);
            Assert.Equal(string.Empty, values["ref_emp_name"]);
            Assert.Equal(Guid.Empty, values["dept_rowid"]);
            Assert.Equal(string.Empty, values["ref_emp_dept_id"]);
        }

        [Fact]
        [DisplayName("A field that is not marked ValueChangeCallback gets an empty result")]
        public void FieldValueChange_UnmarkedField_ReturnsEmpty()
        {
            var result = new Wiring(_fx, DatabaseType.SQLite).CreateBo().FieldValueChange(
                new FieldValueChangeArgs { TableName = ProgId, FieldName = "plain_rowid", Value = Guid.NewGuid().ToString() });

            Assert.Null(result.Values);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        [DisplayName("A table or field the form does not have is refused")]
        public void FieldValueChange_UnknownField_Throws()
        {
            var bo = new Wiring(_fx, DatabaseType.SQLite).CreateBo();

            Assert.Throws<ArgumentException>(() => bo.FieldValueChange(
                new FieldValueChangeArgs { TableName = ProgId, FieldName = "no_such_field" }));
            Assert.Throws<ArgumentException>(() => bo.FieldValueChange(
                new FieldValueChangeArgs { TableName = "NoSuchTable", FieldName = "emp_rowid" }));
            Assert.Throws<ArgumentNullException>(() => bo.FieldValueChange(null!));
        }

        [Fact]
        [DisplayName("On a form with a permission model the caller needs Create or Update, and either one is enough")]
        public void FieldValueChange_PermissionModel_RequiresCreateOrUpdate()
        {
            var denied = new Wiring(_fx, DatabaseType.SQLite, PermissionModelId, new StubAuthorization());
            var updateOnly = new Wiring(_fx, DatabaseType.SQLite, PermissionModelId, new StubAuthorization(PermissionActions.Update));
            var createOnly = new Wiring(_fx, DatabaseType.SQLite, PermissionModelId, new StubAuthorization(PermissionActions.Create));

            Assert.Throws<ForbiddenException>(() => denied.CreateBo().FieldValueChange(Args(string.Empty)));
            Assert.Empty(updateOnly.CreateBo().FieldValueChange(Args(string.Empty)).Errors);
            Assert.Empty(createOnly.CreateBo().FieldValueChange(Args(string.Empty)).Errors);
        }

        [Fact]
        [DisplayName("An override adds its own values and validation messages to what the base returns")]
        public void FieldValueChange_Override_AddsToBaseResult()
        {
            var wiring = new Wiring(_fx, DatabaseType.SQLite);
            var bo = new PricingBo(wiring.Context, wiring.Token, ProgId);

            var result = bo.FieldValueChange(Args(string.Empty));

            Assert.Equal(string.Empty, result.Values!.Rows[0]["ref_emp_id"]);
            Assert.Equal(9.5m, result.Values.Rows[0]["unit_price"]);
            Assert.Equal("Check the price list.", result.Warnings["unit_price"]);
        }

        private const string PermissionModelId = "FvcModel";

        private static FieldValueChangeArgs Args(string value) =>
            new() { TableName = ProgId, FieldName = "emp_rowid", Value = value };

        /// <summary>
        /// The referencing form: <c>emp_rowid</c> relates to Employee and asks for a callback, <c>plain_rowid</c> does
        /// not ask for one.
        /// </summary>
        private static FormSchema BuildSchema(string permissionModelId, bool missingSource)
        {
            var schema = new FormSchema(ProgId, "Value change test") { CategoryId = CategoryId, PermissionModelId = permissionModelId };
            var table = schema.Tables!.Add(ProgId, "Value change test");
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            var employee = table.Fields!.Add("emp_rowid", "Employee", FieldDbType.Guid);
            employee.RelationProgId = "Employee";
            employee.ValueChangeCallback = true;
            employee.RelationFieldMappings!.Add(new FieldMapping("sys_id", "ref_emp_id"));
            employee.RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_emp_name"));
            employee.RelationFieldMappings!.Add(new FieldMapping("dept_rowid", "dept_rowid") { LookupOnly = true });
            employee.RelationFieldMappings!.Add(new FieldMapping("ref_dept_id", "ref_emp_dept_id"));
            if (missingSource)
                employee.RelationFieldMappings!.Add(new FieldMapping("no_such_source", "ref_emp_title"));
            var plain = table.Fields!.Add("plain_rowid", "Plain", FieldDbType.Guid);
            plain.RelationProgId = "Employee";
            plain.RelationFieldMappings!.Add(new FieldMapping("sys_id", "ref_plain_id"));
            table.Fields!.Add("dept_rowid", "Department", FieldDbType.Guid);
            foreach (var name in new[] { "ref_emp_id", "ref_emp_name", "ref_emp_dept_id", "ref_plain_id", "ref_emp_title" })
                table.Fields!.Add(new FormField(name, name, FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add("unit_price", "Unit Price", FieldDbType.Decimal);
            return schema;
        }

        private static void InsertEmployee(Wiring wiring, Guid rowId, string sysId, string sysName, Guid deptRowId)
        {
            var dt = new DataTable();
            dt.Columns.Add("sys_rowid", typeof(Guid));
            dt.Columns.Add("sys_id", typeof(string));
            dt.Columns.Add("sys_name", typeof(string));
            dt.Columns.Add("dept_rowid", typeof(Guid));
            dt.Rows.Add(rowId, sysId, sysName, deptRowId);
            wiring.DbAccess.Execute(new InsertCommandBuilder(wiring.EmployeeSchema, wiring.DbType).Build("Employee", dt.Rows[0]));
        }

        private static void InsertDepartment(Wiring wiring, Guid rowId, string sysId, string sysName)
        {
            var dt = new DataTable();
            dt.Columns.Add("sys_rowid", typeof(Guid));
            dt.Columns.Add("sys_id", typeof(string));
            dt.Columns.Add("sys_name", typeof(string));
            dt.Columns.Add("manager_rowid", typeof(Guid));
            dt.Rows.Add(rowId, sysId, sysName, Guid.Empty);
            wiring.DbAccess.Execute(new InsertCommandBuilder(wiring.DepartmentSchema, wiring.DbType).Build("Department", dt.Rows[0]));
        }

        private static void TryDelete(Wiring wiring, FormSchema schema, string progId, Guid rowId)
        {
            try
            {
                wiring.DbAccess.Execute(new DeleteCommandBuilder(schema, wiring.DbType)
                    .Build(progId, FilterCondition.Equal("sys_rowid", rowId)));
            }
            catch (DbException ex)
            {
                // Cleanup is best effort: a failed seed leaves no row, and the assertion message must not be masked.
                Console.WriteLine($"FormBusinessObjectFieldValueChangeTests: cleanup of {progId}#{rowId} failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Wires a business object of the test-built form to a business object factory whose Employee business objects
        /// read the fixture's company database for the provider under test.
        /// </summary>
        private sealed class Wiring
        {
            public Wiring(SharedDbFixture fx, DatabaseType dbType, string permissionModelId = "",
                ICompanyAuthorizationService? authorization = null, bool missingSource = false)
            {
                DbType = dbType;
                var inner = fx.GetRequiredService<IDefineAccess>();
                var defineAccess = new FormSchemaOverlayDefineAccess(inner, BuildSchema(permissionModelId, missingSource));
                EmployeeSchema = inner.GetFormSchema("Employee");
                DepartmentSchema = inner.GetFormSchema("Department");

                var databaseId = TestDbConventions.GetDatabaseId(dbType, CategoryId);
                DbAccess = fx.NewDbAccess(databaseId);
                var repositoryContext = TestRepositoryContext.Create(fx.GetRequiredService<IDbConnectionManager>(),
                    defineAccess: defineAccess, dbAccessFactory: fx.GetRequiredService<IDbAccessFactory>());
                var employees = new DataFormRepository(repositoryContext, "Employee", EmployeeSchema, databaseId);

                var overrides = new List<(Type, object?)> { (typeof(IRepositoryFactory), new SingleRepositoryFactory(employees)) };
                if (authorization is not null) { overrides.Add((typeof(ICompanyAuthorizationService), authorization)); }
                var services = new TestOverrideServiceProvider(fx.Provider, [.. overrides]);

                var sessions = fx.GetRequiredService<ISessionInfoService>();
                var language = fx.GetRequiredService<ILanguageService>();
                Context = new BusinessObjectContext
                {
                    DefineAccess = defineAccess,
                    SessionInfoService = sessions,
                    LanguageService = language,
                    BoFactory = new BusinessObjectFactory(services, defineAccess, sessions, language,
                        fx.GetRequiredService<IBoTypeResolver>()),
                    Services = services,
                };
                Token = TestSessionFactory.CreateAccessToken(fx);
            }

            public DatabaseType DbType { get; }
            public DbAccess DbAccess { get; }
            public FormSchema EmployeeSchema { get; }
            public FormSchema DepartmentSchema { get; }
            public IBusinessObjectContext Context { get; }
            public Guid Token { get; }

            public FormBusinessObject CreateBo() => new(Context, Token, ProgId);
        }

        private sealed class SingleRepositoryFactory : IRepositoryFactory
        {
            private readonly IDataFormRepository _repository;
            public SingleRepositoryFactory(IDataFormRepository repository) => _repository = repository;
            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_repository;
            public T Create<T>(Guid accessToken = default) where T : class => throw new NotSupportedException();
        }

        private sealed class StubAuthorization : ICompanyAuthorizationService
        {
            private readonly PermissionActions _granted;
            public StubAuthorization(PermissionActions granted = PermissionActions.None) => _granted = granted;
            public bool Can(Guid accessToken, string modelId, PermissionActions action) => (_granted & action) == action;
        }

        private sealed class ExcludingLookupBo : FormBusinessObject
        {
            private readonly FilterNode _filter;
            public ExcludingLookupBo(IBusinessObjectContext ctx, Guid accessToken, string progId, FilterNode filter)
                : base(ctx, accessToken, progId) => _filter = filter;
            protected override FilterNode? GetLookupFilter() => _filter;
        }

        private sealed class PricingBo : FormBusinessObject
        {
            public PricingBo(IBusinessObjectContext ctx, Guid accessToken, string progId) : base(ctx, accessToken, progId) { }

            public override FieldValueChangeResult FieldValueChange(FieldValueChangeArgs args)
            {
                var result = base.FieldValueChange(args);
                if (StringUtilities.IsEquals(args.FieldName, "emp_rowid"))
                {
                    result.SetValue("unit_price", 9.5m);
                    result.Warnings["unit_price"] = "Check the price list.";
                }
                return result;
            }
        }
    }
}
