using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.ObjectCaching.Services
{
    /// <summary>
    /// Layer-1 authorization service. Resolves the session's roles and the company's
    /// role-permission snapshot (both from cache — zero DB on the check path), then OR-merges the
    /// allowed action mask across the user's roles and tests the requested action.
    /// </summary>
    public sealed class CompanyAuthorizationService : ICompanyAuthorizationService
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
        public bool Can(Guid accessToken, string modelId, PermissionActions action)
            => Can(_sessionInfoService.Get(accessToken)?.CompanyScope ?? SessionCompanyScope.None, modelId, action);

        /// <inheritdoc/>
        public bool Can(SessionCompanyScope companyScope, string modelId, PermissionActions action)
        {
            ArgumentNullException.ThrowIfNull(companyScope);
            // The company and the roles come from the same snapshot, so they cannot straddle a company switch.
            if (string.IsNullOrEmpty(companyScope.CompanyId) || companyScope.Roles.Count == 0)
            {
                return false;
            }

            var snapshot = _rolePermissionService.Get(companyScope.CompanyId);
            if (snapshot == null) { return false; }

            return snapshot.GetAllowed(companyScope.Roles, modelId).HasFlag(action);
        }
    }
}
