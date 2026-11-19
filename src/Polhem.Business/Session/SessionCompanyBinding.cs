using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Business.Session
{
    /// <summary>
    /// The outcome of binding a session to a company: the company record and the capability
    /// snapshot computed for the session's roles within it.
    /// </summary>
    /// <param name="Company">The company the session was bound to.</param>
    /// <param name="Capabilities">
    /// The per-model allowed action mask for the session's roles in that company.
    /// </param>
    public sealed record SessionCompanyBinding(
        CompanyInfo Company,
        IReadOnlyDictionary<string, PermissionActions> Capabilities)
    {
        /// <summary>Gets the user's row id the binding resolved (<c>st_user.sys_rowid</c>).</summary>
        public Guid UserRowId { get; init; }

        /// <summary>
        /// Gets the row id of the employee linked to the user in the company; <see cref="Guid.Empty"/> when there is none.
        /// </summary>
        public Guid EmployeeRowId { get; init; }
    }
}
