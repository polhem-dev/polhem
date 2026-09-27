using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Identity
{
    /// <summary>
    /// Behavior regression for <see cref="CompanyRolePermissions"/> after it moved to building its indexes at construction.
    /// </summary>
    /// <remarks>
    /// This is a pure performance refactoring and **the behavior must not change at all**, so these tests check semantics, not speed: OR-merging
    /// across roles, not counting roles the user does not hold, exact action matching, and merging duplicate grants.
    /// </remarks>
    public class CompanyRolePermissionsIndexTests
    {
        private static CompanyRolePermissions Build()
        {
            var grants = new List<RoleGrantRow>
            {
                new("admin",  "order", PermissionActions.Read,   ScopeStrategy.All),
                new("admin",  "order", PermissionActions.Update, ScopeStrategy.Own),
                new("clerk",  "order", PermissionActions.Read,   ScopeStrategy.Dept),
                new("clerk",  "item",  PermissionActions.Read,   ScopeStrategy.All),
                new("other",  "order", PermissionActions.Delete, ScopeStrategy.All),
            };
            var userRoles = new List<UserRoleRow>
            {
                new("u1", "admin"),
                new("u1", "clerk"),
                new("u2", "other"),
            };
            return new CompanyRolePermissions("C1", grants, userRoles);
        }

        [Fact]
        [DisplayName("GetAllowed OR-merges the permissions of several roles on the same model")]
        public void GetAllowed_MergesAcrossRoles()
        {
            var allowed = Build().GetAllowed(["admin", "clerk"], "order");

            Assert.Equal(PermissionActions.Read | PermissionActions.Update, allowed);
        }

        [Fact]
        [DisplayName("GetAllowed does not count roles the user does not hold")]
        public void GetAllowed_IgnoresRolesNotHeld()
        {
            // "other" has Delete on order, but the user holds only clerk, so it must not leak in.
            var allowed = Build().GetAllowed(["clerk"], "order");

            Assert.Equal(PermissionActions.Read, allowed);
            Assert.False(allowed.HasFlag(PermissionActions.Delete));
        }

        [Fact]
        [DisplayName("GetAllowed returns None for a model with no grants")]
        public void GetAllowed_UnknownModel_ReturnsNone()
        {
            Assert.Equal(PermissionActions.None, Build().GetAllowed(["admin"], "nowhere"));
        }

        [Fact]
        [DisplayName("GetAllowedByModel returns the per-model merge for the roles held")]
        public void GetAllowedByModel_MergesPerModel()
        {
            var byModel = Build().GetAllowedByModel(["admin", "clerk"]);

            Assert.Equal(2, byModel.Count);
            Assert.Equal(PermissionActions.Read | PermissionActions.Update, byModel["order"]);
            Assert.Equal(PermissionActions.Read, byModel["item"]);
        }

        [Fact]
        [DisplayName("GetEffectiveScopes matches the action exactly and covers every role held")]
        public void GetEffectiveScopes_MatchesActionExactly()
        {
            var scopes = Build().GetEffectiveScopes(["admin", "clerk"], "order", PermissionActions.Read);

            // The admin Read scope is All and the clerk Read scope is Dept. The admin Update (Own) scope must not mix in.
            Assert.Equal(2, scopes.Count);
            Assert.Contains(ScopeStrategy.All, scopes);
            Assert.Contains(ScopeStrategy.Dept, scopes);
            Assert.DoesNotContain(ScopeStrategy.Own, scopes);
        }

        [Fact]
        [DisplayName("GetUserRoleIds returns all of the user's roles without mixing in other users' roles")]
        public void GetUserRoleIds_ReturnsOnlyThatUsersRoles()
        {
            var roles = Build().GetUserRoleIds("u1");

            Assert.Equal(2, roles.Count);
            Assert.Contains("admin", roles);
            Assert.Contains("clerk", roles);
            Assert.DoesNotContain("other", roles);
        }

        [Fact]
        [DisplayName("GetUserRoleIds returns an empty collection instead of throwing for an unknown user")]
        public void GetUserRoleIds_UnknownUser_ReturnsEmpty()
        {
            Assert.Empty(Build().GetUserRoleIds("nobody"));
        }

        [Fact]
        [DisplayName("Duplicate grants for the same (role, model) merge into a single mask")]
        public void DuplicateGrants_AreMerged()
        {
            var grants = new List<RoleGrantRow>
            {
                new("r", "m", PermissionActions.Read,   ScopeStrategy.All),
                new("r", "m", PermissionActions.Delete, ScopeStrategy.All),
            };
            var perms = new CompanyRolePermissions("C1", grants, []);

            Assert.Equal(PermissionActions.Read | PermissionActions.Delete, perms.GetAllowed(["r"], "m"));
        }
    }
}
