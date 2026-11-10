using Polhem.Definition.Settings;

namespace Polhem.Definition.Identity
{
    /// <summary>
    /// Layer-1 authorization: whether the session's user may perform an action on a permission
    /// model within their current company. Pure <c>(model, action)</c> gate — record-scope
    /// filtering (layer 2) is a separate concern.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: inside a request, call the overload that takes a <see cref="SessionCompanyScope"/> with the
    /// request's one snapshot. The access-token overload reads the session again, and a concurrent
    /// <c>EnterCompany</c> on the same token can change the company between that read and the request's other
    /// reads, pairing one company's roles with another company's data (ADR-063).
    /// </remarks>
    public interface ICompanyAuthorizationService
    {
        /// <summary>
        /// Returns <c>true</c> when the session identified by <paramref name="accessToken"/> is
        /// authorized for <paramref name="action"/> on <paramref name="modelId"/>; otherwise
        /// <c>false</c> (not logged in, no company entered, no roles, or no grant).
        /// </summary>
        /// <param name="accessToken">The session's access token.</param>
        /// <param name="modelId">The permission model id.</param>
        /// <param name="action">The action to check (a single <see cref="PermissionActions"/> flag).</param>
        bool Can(Guid accessToken, string modelId, PermissionActions action);

        /// <summary>
        /// Returns <c>true</c> when <paramref name="companyScope"/> is authorized for <paramref name="action"/> on
        /// <paramref name="modelId"/>; otherwise <c>false</c> (no company entered, no roles, or no grant).
        /// </summary>
        /// <param name="companyScope">The request's snapshot of the session's company scope.</param>
        /// <param name="modelId">The permission model id.</param>
        /// <param name="action">The action to check (a single <see cref="PermissionActions"/> flag).</param>
        bool Can(SessionCompanyScope companyScope, string modelId, PermissionActions action);
    }
}
