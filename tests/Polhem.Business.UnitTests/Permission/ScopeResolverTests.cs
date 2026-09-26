using System.ComponentModel;
using System.Data;
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
    /// Layer-two resolution tests for ScopeResolver (isolated with fake session / role-permission / department-tree /
    /// define-access): each scope strategy → FilterNode, merging several roles (any All means no filter, otherwise OR),
    /// Inherit resolving to the model default, the two Owner identities, the implied Own, fail-closed edges, and per-row IsRowInScope.
    /// </summary>
    public class ScopeResolverTests
    {
        private const string Model = "PurchaseOrder";
        private static readonly Guid s_user = Guid.NewGuid();
        private static readonly Guid s_employee = Guid.NewGuid();
        private static readonly Guid s_dept = Guid.NewGuid();
        private static readonly Guid s_subDept = Guid.NewGuid();

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

        private static DepartmentTree DeptTree()
            => new("C001",
            [
                new DepartmentRow(s_dept, "HQ", "總公司", Guid.Empty, Guid.Empty),
                new DepartmentRow(s_subDept, "SUB", "子部門", s_dept, Guid.Empty),
            ]);

        private static PermissionModels Models(PermissionAction action, ScopeStrategy scope)
        {
            var models = new PermissionModels();
            var model = models.Models!.Add(Model, "採購單");
            model.Rules!.Add(action, scope);
            return models;
        }

        private static ScopeResolver Build(SessionInfo session, List<RoleGrantRow> grants, DepartmentTree? tree = null, PermissionModels? models = null)
        {
            var perms = new CompanyRolePermissions(session.CompanyId!, grants, []);
            return new ScopeResolver(
                new FakeSessionInfoService(session),
                new FakeRolePermissionService(perms),
                new FakeDepartmentTreeService(tree),
                new FakeDefineAccess { PermissionModels = models });
        }

        // ---- read-side: per-strategy predicates ----

        [Fact]
        [DisplayName("Own → owner column IN {UserRowId, EmployeeRowId} (both identities)")]
        public void ResolveFilter_Own_OwnerInBothIdentities()
        {
            var session = Session(s_user, s_employee, Guid.Empty, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Own)]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            AssertIn(node!, "buyer_rowid", s_user, s_employee);
        }

        [Fact]
        [DisplayName("Own for a user with no matching employee compares the owner column with UserRowId only")]
        public void ResolveFilter_Own_NoEmployee_UserOnly()
        {
            var session = Session(s_user, Guid.Empty, Guid.Empty, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Own)]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            AssertIn(node!, "buyer_rowid", s_user);
        }

        [Fact]
        [DisplayName("Dept → (dept column = DeptRowId) OR Own (the implied Own)")]
        public void ResolveFilter_Dept_DeptOrOwn()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Dept)]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            var group = Assert.IsType<FilterGroup>(node);
            Assert.Equal(LogicalOperator.Or, group.Operator);
            Assert.Equal(2, group.Nodes.Count);
            AssertEqual(FindByField(group, "dept_rowid"), "dept_rowid", s_dept);
            AssertIn(FindByField(group, "buyer_rowid"), "buyer_rowid", s_user, s_employee);
        }

        [Fact]
        [DisplayName("DeptAndSub → dept column IN own department and subdepartments OR Own")]
        public void ResolveFilter_DeptAndSub_SubtreeOrOwn()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.DeptAndSub)], DeptTree());

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            var group = Assert.IsType<FilterGroup>(node);
            Assert.Equal(LogicalOperator.Or, group.Operator);
            AssertIn(FindByField(group, "dept_rowid"), "dept_rowid", s_dept, s_subDept);
            AssertIn(FindByField(group, "buyer_rowid"), "buyer_rowid", s_user, s_employee);
        }

        [Fact]
        [DisplayName("Several Dept columns → each dept column IN its subtree joined by OR, then OR Own (the from/to departments of a transfer form)")]
        public void ResolveFilter_MultipleDeptFields_OrUnion()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.DeptAndSub)], DeptTree());

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, TransferSchema());

            // Flat OR: from_dept IN subtree, to_dept IN subtree, buyer_rowid IN identities, so both departments' managers can see it.
            var group = Assert.IsType<FilterGroup>(node);
            Assert.Equal(LogicalOperator.Or, group.Operator);
            Assert.Equal(3, group.Nodes.Count);
            AssertIn(FindByField(group, "from_dept"), "from_dept", s_dept, s_subDept);
            AssertIn(FindByField(group, "to_dept"), "to_dept", s_dept, s_subDept);
            AssertIn(FindByField(group, "buyer_rowid"), "buyer_rowid", s_user, s_employee);
        }

        // A transfer form: master marks two Dept columns (from / to) plus one Owner.
        private static FormSchema TransferSchema()
        {
            var schema = new FormSchema { ProgId = "PO001", PermissionModelId = Model };
            var table = new FormTable("PO001", "調職單");
            table.Fields!.Add(new FormField { FieldName = "sys_rowid" });
            table.Fields.Add(new FormField { FieldName = "buyer_rowid", ScopeRole = ScopeRole.Owner });
            table.Fields.Add(new FormField { FieldName = "from_dept", ScopeRole = ScopeRole.Dept });
            table.Fields.Add(new FormField { FieldName = "to_dept", ScopeRole = ScopeRole.Dept });
            schema.Tables!.Add(table);
            return schema;
        }

        // ---- read-side: multi-role merge ----

        [Fact]
        [DisplayName("Several roles with any All → no filter (null)")]
        public void ResolveFilter_AnyAll_ReturnsNull()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer", "Manager");
            var resolver = Build(session,
            [
                new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Dept),
                new("Manager", Model, PermissionAction.Read, ScopeStrategy.All),
            ]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            Assert.Null(node);
        }

        [Fact]
        [DisplayName("Several roles with different scopes (no All) → the predicates are joined by OR")]
        public void ResolveFilter_MultiRole_OrUnion()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer", "Clerk");
            var resolver = Build(session,
            [
                new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Own),
                new("Clerk", Model, PermissionAction.Read, ScopeStrategy.Dept),
            ]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            // Top-level OR: the Own branch plus the Dept branch (Dept itself is a group of (dept=..) OR Own).
            var group = Assert.IsType<FilterGroup>(node);
            Assert.Equal(LogicalOperator.Or, group.Operator);
            Assert.Equal(2, group.Nodes.Count);
        }

        // ---- read-side: Inherit → model default ----

        [Fact]
        [DisplayName("Inherit → resolves to the model's default scope (Dept)")]
        public void ResolveFilter_Inherit_ResolvesModelDefault()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session,
                [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Inherit)],
                models: Models(PermissionAction.Read, ScopeStrategy.Dept));

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            var group = Assert.IsType<FilterGroup>(node);
            AssertEqual(FindByField(group, "dept_rowid"), "dept_rowid", s_dept);
        }

        [Fact]
        [DisplayName("Inherit → model default All → no filter (null)")]
        public void ResolveFilter_Inherit_ModelDefaultAll_ReturnsNull()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session,
                [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Inherit)],
                models: Models(PermissionAction.Read, ScopeStrategy.All));

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema());

            Assert.Null(node);
        }

        // ---- read-side: fail-closed edges ----

        [Fact]
        [DisplayName("Own without an owner column → fail-closed (always false, 1=0)")]
        public void ResolveFilter_Own_NoOwnerField_DeniesAll()
        {
            var session = Session(s_user, s_employee, Guid.Empty, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Own)]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema(owner: false));

            AssertDenyAll(node!);
        }

        [Fact]
        [DisplayName("Dept without a dept column or an owner column → fail-closed")]
        public void ResolveFilter_Dept_NoFields_DeniesAll()
        {
            var session = Session(s_user, s_employee, s_dept, "Buyer");
            var resolver = Build(session, [new("Buyer", Model, PermissionAction.Read, ScopeStrategy.Dept)]);

            var node = resolver.ResolveFilter(session.AccessToken, Model, PermissionAction.Read, Schema(owner: false, dept: false));

            AssertDenyAll(node!);
        }

        // ---- helpers ----

        private static FilterCondition FindByField(FilterGroup group, string field)
        {
            foreach (var node in group.Nodes)
            {
                if (node is FilterCondition c && c.FieldName == field) { return c; }
            }
            throw new Xunit.Sdk.XunitException($"No condition on field '{field}' found in group.");
        }

        private static void AssertIn(FilterNode node, string field, params Guid[] expected)
        {
            var c = Assert.IsType<FilterCondition>(node);
            Assert.Equal(field, c.FieldName);
            Assert.Equal(ComparisonOperator.In, c.Operator);
            var values = ((IEnumerable<object>)c.Value!).Cast<Guid>().ToList();
            Assert.Equal(expected.Length, values.Count);
            foreach (var e in expected) { Assert.Contains(e, values); }
        }

        private static void AssertEqual(FilterNode node, string field, Guid expected)
        {
            var c = Assert.IsType<FilterCondition>(node);
            Assert.Equal(field, c.FieldName);
            Assert.Equal(ComparisonOperator.Equal, c.Operator);
            Assert.Equal(expected, (Guid)c.Value!);
        }

        private static void AssertDenyAll(FilterNode node)
        {
            var c = Assert.IsType<FilterCondition>(node);
            Assert.Equal(ComparisonOperator.In, c.Operator);
            Assert.Empty((IEnumerable<object>)c.Value!);
        }

        private sealed class FakeSessionInfoService : ISessionInfoService
        {
            private readonly SessionInfo _session;
            public FakeSessionInfoService(SessionInfo session) { _session = session; }
            public SessionInfo Get(Guid accessToken) => _session;
            public void Set(SessionInfo sessionInfo) { }
            public void Remove(Guid accessToken) { }
        }

        private sealed class FakeRolePermissionService : IRolePermissionService
        {
            private readonly CompanyRolePermissions _perms;
            public FakeRolePermissionService(CompanyRolePermissions perms) { _perms = perms; }
            public CompanyRolePermissions? Get(string companyId) => _perms;
            public void Remove(string companyId) { }
        }

        private sealed class FakeDepartmentTreeService : IDepartmentTreeService
        {
            private readonly DepartmentTree? _tree;
            public FakeDepartmentTreeService(DepartmentTree? tree) { _tree = tree; }
            public DepartmentTree? Get(string companyId) => _tree;
            public void Remove(string companyId) { }
        }
    }
}
