using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.ObjectCaching.Services;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// <c>CompanyAuthorizationService.Can</c> must decide from one company scope even when the session
    /// switches company while the check runs, because the session instance is shared by every request
    /// carrying its token.
    /// </summary>
    public class CompanyAuthorizationScopeSnapshotTests
    {
        private static readonly Guid s_token = Guid.NewGuid();

        /// <summary>
        /// Returns the permission snapshot and, while doing so, switches the session to another company,
        /// which is the point where a concurrent <c>EnterCompany</c> would land.
        /// </summary>
        private sealed class SwitchingRolePermissionService(SessionInfo session, CompanyRolePermissions perms)
            : IRolePermissionService
        {
            public CompanyRolePermissions? Get(string companyId)
            {
                session.CompanyScope = new SessionCompanyScope("C002", string.Empty, ["Clerk"], Guid.Empty, Guid.Empty, Guid.Empty);
                return perms;
            }

            public void Remove(string companyId) { }
        }

        private sealed class SingleSessionService(SessionInfo session) : ISessionInfoService
        {
            public SessionInfo Get(Guid accessToken) => session;
            public void Set(SessionInfo sessionInfo) { }
            public void Remove(Guid accessToken) { }
        }

        [Fact]
        [DisplayName("Can evaluates the roles of the company it looked up, even if the session switches company mid-check")]
        public void Can_CompanySwitchDuringCheck_UsesRolesOfTheSameSnapshot()
        {
            var session = new SessionInfo { AccessToken = s_token, UserId = "001", CompanyId = "C001", Roles = ["Buyer"] };
            var perms = new CompanyRolePermissions(
                "C001", [new RoleGrantRow("Buyer", "PurchaseOrder", PermissionActions.Read, ScopeStrategy.All)], []);
            var auth = new CompanyAuthorizationService(
                new SingleSessionService(session), new SwitchingRolePermissionService(session, perms));

            bool allowed = auth.Can(s_token, "PurchaseOrder", PermissionActions.Read);

            Assert.True(allowed);
            Assert.Equal("C002", session.CompanyId);
        }
    }
}
