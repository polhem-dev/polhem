using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.ObjectCaching.Services;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// Tests of the layer-one decision chain of <c>CompanyAuthorizationService.Can</c>, isolated with a fake session
    /// and role-permission service: the empty checks, the OR merge across roles, and <c>HasFlag</c>.
    /// </summary>
    public class CompanyAuthorizationServiceTests
    {
        private static readonly Guid s_token = Guid.NewGuid();

        private static CompanyRolePermissions BuildPerms()
        {
            var grants = new List<RoleGrantRow>
            {
                new("Buyer", "PurchaseOrder", PermissionActions.Read, ScopeStrategy.All),
                new("Buyer", "PurchaseOrder", PermissionActions.Update, ScopeStrategy.All),
                new("Manager", "PurchaseOrder", PermissionActions.Delete, ScopeStrategy.All),
            };
            return new CompanyRolePermissions("C001", grants, []);
        }

        private static SessionInfo Session(string? companyId, params string[] roles)
            => new() { AccessToken = s_token, UserId = "001", CompanyId = companyId, Roles = roles.ToList() };

        private static CompanyAuthorizationService Create(SessionInfo? session, CompanyRolePermissions? perms)
            => new(new FakeSessionInfoService(session), new FakeRolePermissionService(perms));

        [Fact]
        [DisplayName("Can returns true for a granted action")]
        public void Can_GrantedAction_ReturnsTrue()
        {
            var auth = Create(Session("C001", "Buyer"), BuildPerms());

            Assert.True(auth.Can(s_token, "PurchaseOrder", PermissionActions.Read));
        }

        [Fact]
        [DisplayName("Can returns false for an action that was not granted")]
        public void Can_UngrantedAction_ReturnsFalse()
        {
            var auth = Create(Session("C001", "Buyer"), BuildPerms());

            // Buyer has no Delete; only Manager has it.
            Assert.False(auth.Can(s_token, "PurchaseOrder", PermissionActions.Delete));
        }

        [Fact]
        [DisplayName("Can merges the permissions of several roles on the same model with OR before deciding")]
        public void Can_MultiRole_OrMerges()
        {
            var auth = Create(Session("C001", "Buyer", "Manager"), BuildPerms());

            // Buyer (Read | Update) OR Manager (Delete) gives Delete.
            Assert.True(auth.Can(s_token, "PurchaseOrder", PermissionActions.Delete));
        }

        [Fact]
        [DisplayName("Can returns false when no company has been entered (CompanyId is null)")]
        public void Can_NoCompany_ReturnsFalse()
        {
            var auth = Create(Session(null, "Buyer"), BuildPerms());

            Assert.False(auth.Can(s_token, "PurchaseOrder", PermissionActions.Read));
        }

        [Fact]
        [DisplayName("Can returns false when the session has no roles")]
        public void Can_NoRoles_ReturnsFalse()
        {
            var auth = Create(Session("C001"), BuildPerms());

            Assert.False(auth.Can(s_token, "PurchaseOrder", PermissionActions.Read));
        }

        [Fact]
        [DisplayName("Can returns false when the session does not exist")]
        public void Can_NoSession_ReturnsFalse()
        {
            var auth = Create(null, BuildPerms());

            Assert.False(auth.Can(s_token, "PurchaseOrder", PermissionActions.Read));
        }

        private sealed class FakeSessionInfoService : ISessionInfoService
        {
            private readonly SessionInfo? _session;
            public FakeSessionInfoService(SessionInfo? session) { _session = session; }
            public SessionInfo Get(Guid accessToken) => _session!;
            public void Set(SessionInfo sessionInfo) { }
            public void Remove(Guid accessToken) { }
        }

        private sealed class FakeRolePermissionService : IRolePermissionService
        {
            private readonly CompanyRolePermissions? _perms;
            public FakeRolePermissionService(CompanyRolePermissions? perms) { _perms = perms; }
            public CompanyRolePermissions? Get(string companyId) => _perms;
            public void Remove(string companyId) { }
        }
    }
}
