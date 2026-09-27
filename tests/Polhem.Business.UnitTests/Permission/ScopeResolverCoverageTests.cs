using System.ComponentModel;
using Polhem.Business.Permission;
using Polhem.Business.UnitTests.Fakes;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Organization;
using Polhem.Definition.Settings;

namespace Polhem.Business.UnitTests.Permission
{
    /// <summary>
    /// Gap-filling coverage for <see cref="ScopeResolver"/>: the constructor null guards, every fail-closed entry (null session /
    /// null snapshot / no grant), Inherit resolution (model default Inherit → falls back to the Read default / model not registered →
    /// All), the early returns of Dept/DeptAndSub for an empty department and an empty subtree, and each branch of DenyAll's AnyMasterFieldName.
    /// </summary>
    public class ScopeResolverCoverageTests
    {
        private const string Model = "PurchaseOrder";
        private static readonly Guid s_user = Guid.NewGuid();
        private static readonly Guid s_employee = Guid.NewGuid();
        private static readonly Guid s_dept = Guid.NewGuid();

        // ---- builders ----

        private static SessionInfo Session(Guid user, Guid employee, Guid dept, params string[] roles)
            => new()
            {
                AccessToken = Guid.NewGuid(),
                CompanyId = "C001",
                Roles = roles.ToList(),
                UserRowId = user,
                EmployeeRowId = employee,
                DeptRowId = dept,
            };

        private static FormSchema Schema(bool owner = true, bool dept = true)
        {
            var schema = new FormSchema { ProgId = "PO001", PermissionModelId = Model };
            var table = new FormTable("PO001", "採購單");
            table.Fields!.Add(new FormField { FieldName = "sys_rowid" });
            if (owner) { table.Fields.Add(new FormField { FieldName = "buyer_rowid", ScopeRole = ScopeRole.Owner }); }
            if (dept) { table.Fields.Add(new FormField { FieldName = "dept_rowid", ScopeRole = ScopeRole.Dept }); }
            schema.Tables!.Add(table);
            return schema;
        }

        // A master table with no fields at all → AnyMasterFieldName falls through to "sys_rowid".
        private static FormSchema EmptyFieldSchema()
        {
            var schema = new FormSchema { ProgId = "PO001", PermissionModelId = Model };
            schema.Tables!.Add(new FormTable("PO001", "空表"));
            return schema;
        }

        private static DepartmentTree DeptTree()
            => new("C001",
            [
                new DepartmentRow(s_dept, "HQ", "總公司", Guid.Empty, Guid.Empty),
            ]);

        private static ScopeResolver Build(SessionInfo? session, List<RoleGrantRow>? grants,
            DepartmentTree? tree = null, PermissionModels? models = null, bool nullSnapshot = false)
        {
            var perms = nullSnapshot
                ? null
                : new CompanyRolePermissions("C001", grants ?? [], []);
            return new ScopeResolver(
                new CovSessionService(session),
                new CovRoleService(perms),
                new CovDeptService(tree),
                new FakeDefineAccess { PermissionModels = models });
        }

        // ---- constructor null guards ----

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException when any dependency is null")]
        public void Ctor_NullDependencies_Throws()
        {
            var session = new CovSessionService(null);
            var role = new CovRoleService(null);
            var dept = new CovDeptService(null);
            var define = new FakeDefineAccess();

            Assert.Throws<ArgumentNullException>(() => new ScopeResolver(null!, role, dept, define));
            Assert.Throws<ArgumentNullException>(() => new ScopeResolver(session, null!, dept, define));
            Assert.Throws<ArgumentNullException>(() => new ScopeResolver(session, role, null!, define));
            Assert.Throws<ArgumentNullException>(() => new ScopeResolver(session, role, dept, null!));
        }

        // ---- fail-closed entry points ----

        [Fact]
        [DisplayName("No session (null) → DenyAll (on the owner column)")]
        public void ResolveFilter_NoSession_DeniesAll()
        {
            var resolver = Build(session: null, grants: []);

            var node = resolver.ResolveFilter(Guid.NewGuid(), Model, PermissionActions.Read, Schema());

            AssertDenyAll(node!, "buyer_rowid");
        }

        [Fact]
        [DisplayName("A null company permission snapshot → DenyAll")]
        public void ResolveFilter_NullSnapshot_DeniesAll()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, grants: null, nullSnapshot: true);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Read, Schema());

            AssertDenyAll(node!, "buyer_rowid");
        }

        [Fact]
        [DisplayName("The roles have no grant for that model/action → DenyAll")]
        public void ResolveFilter_NoGrant_DeniesAll()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, grants: []);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Read, Schema());

            AssertDenyAll(node!, "buyer_rowid");
        }

        [Fact]
        [DisplayName("DenyAll: no owner column but a dept column → the always-false condition goes on the dept column")]
        public void ResolveFilter_NoGrant_DeptFieldOnly_DeniesOnDept()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, grants: []);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Read, Schema(owner: false));

            AssertDenyAll(node!, "dept_rowid");
        }

        [Fact]
        [DisplayName("DenyAll: the master has no columns → the always-false condition goes on sys_rowid (the fallback)")]
        public void ResolveFilter_NoGrant_EmptyMaster_DeniesOnSysRowId()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, grants: []);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Read, EmptyFieldSchema());

            AssertDenyAll(node!, "sys_rowid");
        }

        // ---- Inherit resolution ----

        [Fact]
        [DisplayName("Inherit (Print) → the action has no default → falls back to the model's Read default (Dept)")]
        public void ResolveFilter_InheritFallsBackToReadDefault()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var models = new PermissionModels();
            var model = models.Models!.Add(Model, "採購單");
            model.Rules!.Add(PermissionActions.Read, ScopeStrategy.Dept);

            var resolver = Build(session,
                [new("Buyer", Model, PermissionActions.Print, ScopeStrategy.Inherit)],
                tree: DeptTree(), models: models);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Print, Schema());

            // Print inherits Read=Dept → (dept=..) OR Own group.
            var group = Assert.IsType<FilterGroup>(node);
            Assert.Equal(LogicalOperator.Or, group.Operator);
        }

        [Fact]
        [DisplayName("Inherit (Print) → the model is registered but has no rules → both levels Inherit → All (no filter)")]
        public void ResolveFilter_InheritNoRules_ResolvesAll()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var models = new PermissionModels();
            models.Models!.Add(Model, "採購單"); // No rules.

            var resolver = Build(session,
                [new("Buyer", Model, PermissionActions.Print, ScopeStrategy.Inherit)],
                models: models);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Print, Schema());

            Assert.Null(node);
        }

        [Fact]
        [DisplayName("Inherit (Print) → the model is not registered → ModelDefault returns Inherit → All (no filter)")]
        public void ResolveFilter_InheritModelNotRegistered_ResolvesAll()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var models = new PermissionModels();
            models.Models!.Add("OtherModel", "他"); // Does not contain the target model.

            var resolver = Build(session,
                [new("Buyer", Model, PermissionActions.Print, ScopeStrategy.Inherit)],
                models: models);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Print, Schema());

            Assert.Null(node);
        }

        // ---- Dept / DeptAndSub early-return guards ----

        [Fact]
        [DisplayName("Dept scope but the session has no department (Empty) → only the Own branch remains")]
        public void ResolveFilter_DeptScope_NoDept_OwnOnly()
        {
            var session = Session(s_user, s_employee, Guid.Empty, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionActions.Read, ScopeStrategy.Dept)]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Read, Schema());

            var c = Assert.IsType<FilterCondition>(node);
            Assert.Equal("buyer_rowid", c.FieldName);
            Assert.Equal(ComparisonOperator.In, c.Operator);
        }

        [Fact]
        [DisplayName("DeptAndSub but the schema has no dept column → subtree expansion is skipped and only Own remains")]
        public void ResolveFilter_DeptAndSub_NoDeptField_OwnOnly()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session,
                [new("Buyer", Model, PermissionActions.Read, ScopeStrategy.DeptAndSub)], tree: DeptTree());

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Read, Schema(dept: false));

            var c = Assert.IsType<FilterCondition>(node);
            Assert.Equal("buyer_rowid", c.FieldName);
        }

        [Fact]
        [DisplayName("DeptAndSub but the department tree service returns null (empty subtree) → subtree expansion is skipped and only Own remains")]
        public void ResolveFilter_DeptAndSub_NullTree_OwnOnly()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session,
                [new("Buyer", Model, PermissionActions.Read, ScopeStrategy.DeptAndSub)], tree: null);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionActions.Read, Schema());

            var c = Assert.IsType<FilterCondition>(node);
            Assert.Equal("buyer_rowid", c.FieldName);
        }

        // ---- helpers ----

        private static void AssertDenyAll(FilterNode node, string expectedField)
        {
            var c = Assert.IsType<FilterCondition>(node);
            Assert.Equal(expectedField, c.FieldName);
            Assert.Equal(ComparisonOperator.In, c.Operator);
            Assert.Empty((IEnumerable<object>)c.Value!);
        }

        private sealed class CovSessionService : ISessionInfoService
        {
            private readonly SessionInfo? _session;
            public CovSessionService(SessionInfo? session) { _session = session; }
            public SessionInfo Get(Guid accessToken) => _session!;
            public void Set(SessionInfo sessionInfo) { }
            public void Remove(Guid accessToken) { }
        }

        private sealed class CovRoleService : IRolePermissionService
        {
            private readonly CompanyRolePermissions? _perms;
            public CovRoleService(CompanyRolePermissions? perms) { _perms = perms; }
            public CompanyRolePermissions? Get(string companyId) => _perms;
            public void Remove(string companyId) { }
        }

        private sealed class CovDeptService : IDepartmentTreeService
        {
            private readonly DepartmentTree? _tree;
            public CovDeptService(DepartmentTree? tree) { _tree = tree; }
            public DepartmentTree? Get(string companyId) => _tree;
            public void Remove(string companyId) { }
        }
    }
}
