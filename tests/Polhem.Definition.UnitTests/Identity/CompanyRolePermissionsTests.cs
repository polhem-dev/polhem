using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Identity
{
    /// <summary>
    /// Tests for the tier-one decision logic of CompanyRolePermissions (OR-merging across roles, no grant, user to role).
    /// </summary>
    public class CompanyRolePermissionsTests
    {
        private static readonly string s_user = "U001";
        private static readonly string[] s_buyer = { "Buyer" };
        private static readonly string[] s_buyerManager = { "Buyer", "Manager" };

        private static CompanyRolePermissions Build()
        {
            var grants = new List<RoleGrantRow>
            {
                new("Buyer", "PurchaseOrder", PermissionActions.Read, ScopeStrategy.Dept),
                new("Buyer", "PurchaseOrder", PermissionActions.Update, ScopeStrategy.Own),
                new("Buyer", "Vendor", PermissionActions.Read, ScopeStrategy.All),
                new("Manager", "PurchaseOrder", PermissionActions.Read, ScopeStrategy.All),
                new("Manager", "PurchaseOrder", PermissionActions.Delete, ScopeStrategy.Inherit),
            };
            var userRoles = new List<UserRoleRow>
            {
                new(s_user, "Buyer"),
                new(s_user, "Manager"),
            };
            return new CompanyRolePermissions("C001", grants, userRoles);
        }

        [Fact]
        [DisplayName("GetAllowed returns the action mask of a single role")]
        public void GetAllowed_SingleRole_ReturnsMask()
        {
            var perms = Build();

            var allowed = perms.GetAllowed(s_buyer, "PurchaseOrder");

            Assert.Equal(PermissionActions.Read | PermissionActions.Update, allowed);
        }

        [Fact]
        [DisplayName("GetAllowed OR-merges several roles on the same model (capabilities add up)")]
        public void GetAllowed_MultiRole_OrMerges()
        {
            var perms = Build();

            // Buyer(Read|Update) ∪ Manager(Delete) on PurchaseOrder
            var allowed = perms.GetAllowed(s_buyerManager, "PurchaseOrder");

            Assert.Equal(PermissionActions.Read | PermissionActions.Update | PermissionActions.Delete, allowed);
            Assert.True(allowed.HasFlag(PermissionActions.Delete));
        }

        [Fact]
        [DisplayName("GetAllowed returns None for a model with no grant")]
        public void GetAllowed_UnauthorizedModel_ReturnsNone()
        {
            var perms = Build();

            var allowed = perms.GetAllowed(s_buyerManager, "Requisition");

            Assert.Equal(PermissionActions.None, allowed);
            Assert.False(allowed.HasFlag(PermissionActions.Read));
        }

        [Fact]
        [DisplayName("GetAllowed counts only the roles the user holds and excludes grants of other roles")]
        public void GetAllowed_OnlyHeldRoles_ExcludesOthers()
        {
            var perms = Build();

            // Only Buyer is held, so Manager's Delete is not included.
            var allowed = perms.GetAllowed(s_buyer, "PurchaseOrder");

            Assert.False(allowed.HasFlag(PermissionActions.Delete));
        }

        [Fact]
        [DisplayName("GetAllowedByModel returns the OR-merged mask for each model (roles add up)")]
        public void GetAllowedByModel_MultiRole_OrMergesPerModel()
        {
            var perms = Build();

            var map = perms.GetAllowedByModel(s_buyerManager);

            Assert.Equal(PermissionActions.Read | PermissionActions.Update | PermissionActions.Delete, map["PurchaseOrder"]);
            Assert.Equal(PermissionActions.Read, map["Vendor"]);
        }

        [Fact]
        [DisplayName("GetAllowedByModel contains only models the user has grants for, and models without a grant do not appear")]
        public void GetAllowedByModel_OnlyGrantedModels()
        {
            var perms = Build();

            var map = perms.GetAllowedByModel(s_buyer);

            Assert.True(map.ContainsKey("PurchaseOrder"));
            Assert.True(map.ContainsKey("Vendor"));
            Assert.False(map.ContainsKey("Requisition"));
            // Only Buyer is held, so PurchaseOrder does not include Manager's Delete.
            Assert.False(map["PurchaseOrder"].HasFlag(PermissionActions.Delete));
        }

        [Fact]
        [DisplayName("GetAllowedByModel returns an empty dictionary for no roles")]
        public void GetAllowedByModel_NoRoles_ReturnsEmpty()
        {
            var perms = Build();

            var map = perms.GetAllowedByModel([]);

            Assert.Empty(map);
        }

        [Fact]
        [DisplayName("GetEffectiveScopes returns the scope of a single role for the (model, action)")]
        public void GetEffectiveScopes_SingleRole_ReturnsScope()
        {
            var perms = Build();

            var scopes = perms.GetEffectiveScopes(s_buyer, "PurchaseOrder", PermissionActions.Read);

            Assert.Equal(new[] { ScopeStrategy.Dept }, scopes);
        }

        [Fact]
        [DisplayName("GetEffectiveScopes returns one scope per role for the same (model, action), to be merged")]
        public void GetEffectiveScopes_MultiRole_ReturnsEach()
        {
            var perms = Build();

            // Buyer Read is Dept and Manager Read is All, so there are two scopes to merge (the resolver applies "any All means no filter").
            var scopes = perms.GetEffectiveScopes(s_buyerManager, "PurchaseOrder", PermissionActions.Read);

            Assert.Equal(2, scopes.Count);
            Assert.Contains(ScopeStrategy.Dept, scopes);
            Assert.Contains(ScopeStrategy.All, scopes);
        }

        [Fact]
        [DisplayName("GetEffectiveScopes allows different scopes for different actions of the same role (view and edit ranges differ)")]
        public void GetEffectiveScopes_PerAction_Differs()
        {
            var perms = Build();

            // Buyer on PurchaseOrder has Read as Dept (can view the department) and Update as Own (can edit only their own).
            Assert.Equal(new[] { ScopeStrategy.Dept }, perms.GetEffectiveScopes(s_buyer, "PurchaseOrder", PermissionActions.Read));
            Assert.Equal(new[] { ScopeStrategy.Own }, perms.GetEffectiveScopes(s_buyer, "PurchaseOrder", PermissionActions.Update));
        }

        [Fact]
        [DisplayName("GetEffectiveScopes returns empty for an action that is not granted")]
        public void GetEffectiveScopes_Ungranted_ReturnsEmpty()
        {
            var perms = Build();

            // Buyer has no `PurchaseOrder.Delete` grant.
            Assert.Empty(perms.GetEffectiveScopes(s_buyer, "PurchaseOrder", PermissionActions.Delete));
        }

        [Fact]
        [DisplayName("GetUserRoleIds returns the roles assigned to the user")]
        public void GetUserRoleIds_ReturnsAssignedRoles()
        {
            var perms = Build();

            var roles = perms.GetUserRoleIds(s_user);

            Assert.Equal(2, roles.Count);
            Assert.Contains("Buyer", roles);
            Assert.Contains("Manager", roles);
        }

        [Fact]
        [DisplayName("GetUserRoleIds returns empty for a user with no assignment")]
        public void GetUserRoleIds_UnknownUser_ReturnsEmpty()
        {
            var perms = Build();

            var roles = perms.GetUserRoleIds("UNKNOWN");

            Assert.Empty(roles);
        }
    }
}
