using Polhem.Definition.Identity;

namespace Polhem.Repository.Abstractions.System
{
    /// <summary>
    /// Data access for per-company permission tables (<c>st_role</c> / <c>st_role_grant</c> /
    /// <c>st_user_role</c>), all resident in a company database. Loads the raw rows used by the
    /// permission cache to build a company's role→grant and user→role maps.
    /// </summary>
    public interface IRolePermissionRepository
    {
        /// <summary>
        /// Gets all role grants in the given company database (<c>st_role_grant</c> joined to
        /// <c>st_role</c> for the role business id).
        /// </summary>
        /// <param name="databaseId">The company database id.</param>
        /// <param name="companyId">The company whose rows are read; a company database may hold several companies.</param>
        IReadOnlyList<RoleGrantRow> GetRoleGrants(string databaseId, string companyId);

        /// <summary>
        /// Gets all user-role assignments in the given company database (<c>st_user_role</c>
        /// joined to <c>st_role</c> for the role business id).
        /// </summary>
        /// <param name="databaseId">The company database id.</param>
        /// <param name="companyId">The company whose rows are read; a company database may hold several companies.</param>
        IReadOnlyList<UserRoleRow> GetUserRoles(string databaseId, string companyId);
    }
}
