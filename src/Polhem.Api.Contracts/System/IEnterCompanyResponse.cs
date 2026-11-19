using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Api.Contracts.System
{
    /// <summary>
    /// Contract interface for the EnterCompany response.
    /// </summary>
    public interface IEnterCompanyResponse
    {
        /// <summary>
        /// Gets the company information that was bound to the session.
        /// </summary>
        CompanyInfo Company { get; }

        /// <summary>
        /// Gets the per-model allowed action mask (the capability snapshot) for the session's roles
        /// in the entered company. The client caches this and its element capability resolver reads
        /// it to degrade toolbar commands, grid actions, and sensitive fields. A model absent from
        /// the map means no permission.
        /// </summary>
        IReadOnlyDictionary<string, PermissionActions> Capabilities { get; }

        /// <summary>
        /// Gets the signed-in user's row id (<c>st_user.sys_rowid</c>), which a front end resolves a "me" query
        /// condition to.
        /// </summary>
        /// <remarks>
        /// Has a default body so an implementation written before the member existed keeps compiling; it reports
        /// <see cref="Guid.Empty"/>, which a front end treats as unknown.
        /// </remarks>
        Guid UserRowId => Guid.Empty;

        /// <summary>
        /// Gets the row id of the employee linked to the signed-in user in the entered company
        /// (<c>st_employee.sys_rowid</c>); <see cref="Guid.Empty"/> when the user has none there.
        /// </summary>
        /// <remarks>
        /// Has a default body so an implementation written before the member existed keeps compiling.
        /// </remarks>
        Guid EmployeeRowId => Guid.Empty;
    }
}
