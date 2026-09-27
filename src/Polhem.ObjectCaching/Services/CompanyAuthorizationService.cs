using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.ObjectCaching.Services
{
    /// <summary>
    /// Layer-1 authorization service. Resolves the session's roles and the company's
    /// role-permission snapshot (both from cache — zero DB on the check path), then OR-merges the
    /// allowed action mask across the user's roles and tests the requested action.
    /// </summary>
    public class CompanyAuthorizationService : ICompanyAuthorizationService
    {
        private readonly ISessionInfoService _sessionInfoService;
        private readonly IRolePermissionService _rolePermissionService;

        /// <summary>
        /// Initializes a new <see cref="CompanyAuthorizationService"/>.
        /// </summary>
        /// <param name="sessionInfoService">Provides the session (user id, company id, roles).</param>
        /// <param name="rolePermissionService">Provides the company's role-permission snapshot.</param>
        public CompanyAuthorizationService(ISessionInfoService sessionInfoService, IRolePermissionService rolePermissionService)
        {
            _sessionInfoService = sessionInfoService ?? throw new ArgumentNullException(nameof(sessionInfoService));
            _rolePermissionService = rolePermissionService ?? throw new ArgumentNullException(nameof(rolePermissionService));
        }

        /// <inheritdoc/>
        public bool Can(Guid accessToken, string modelId, PermissionAction action)
        {
            // One snapshot for the whole check: the company and the roles must come from the same
            // company entry, and a concurrent EnterCompany / LeaveCompany swaps the whole scope.
            var scope = _sessionInfoService.Get(accessToken)?.CompanyScope;
            if (scope == null || string.IsNullOrEmpty(scope.CompanyId) || scope.Roles.Count == 0)
            {
                return false;
            }

            var snapshot = _rolePermissionService.Get(scope.CompanyId);
            if (snapshot == null) { return false; }

            return snapshot.GetAllowed(scope.Roles, modelId).HasFlag(action);
        }
    }
}
