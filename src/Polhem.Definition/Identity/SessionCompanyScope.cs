using System.Collections.ObjectModel;

namespace Polhem.Definition.Identity
{
    /// <summary>
    /// The company-scoped part of a session: the entered company and everything snapshotted from it
    /// that authorization reads. Immutable, so a session switches company by swapping one reference.
    /// </summary>
    /// <remarks>
    /// One <see cref="SessionInfo"/> instance is shared by every concurrent request carrying its
    /// access token. When these values were separate settable properties, a request running while
    /// another one entered a company could pair the new <see cref="CompanyId"/> with the previous
    /// company's <see cref="Roles"/>, the two inputs layer-1 and layer-2 authorization read. A reader
    /// that needs more than one of these values takes <see cref="SessionInfo.CompanyScope"/> once and
    /// reads them all from that snapshot.
    /// </remarks>
    public sealed class SessionCompanyScope
    {
        /// <summary>
        /// Gets the scope of a session that has not entered a company.
        /// </summary>
        public static SessionCompanyScope None { get; } =
            new(null, string.Empty, Array.Empty<string>(), Guid.Empty, Guid.Empty, Guid.Empty);

        /// <summary>
        /// Initializes a new <see cref="SessionCompanyScope"/>.
        /// </summary>
        /// <param name="companyId">The entered company, or <c>null</c> when none is entered.</param>
        /// <param name="customizeId">The company's customization code; empty for the standard deployment.</param>
        /// <param name="roles">The role ids the user holds in the company; copied, so later changes to the argument are not seen.</param>
        /// <param name="userRowId">The user's row id in the company.</param>
        /// <param name="employeeRowId">The user's linked employee row id, or <see cref="Guid.Empty"/>.</param>
        /// <param name="deptRowId">The employee's department row id, or <see cref="Guid.Empty"/>.</param>
        public SessionCompanyScope(
            string? companyId,
            string customizeId,
            IEnumerable<string> roles,
            Guid userRowId,
            Guid employeeRowId,
            Guid deptRowId)
        {
            ArgumentNullException.ThrowIfNull(roles);
            CompanyId = companyId;
            CustomizeId = customizeId ?? string.Empty;
            Roles = new ReadOnlyCollection<string>([.. roles]);
            UserRowId = userRowId;
            EmployeeRowId = employeeRowId;
            DeptRowId = deptRowId;
        }

        /// <summary>
        /// Gets the ID of the entered company; <c>null</c> means the user has logged in but has not
        /// entered a company.
        /// </summary>
        public string? CompanyId { get; }

        /// <summary>
        /// Gets the tenant customization code derived from the company; empty for the standard deployment.
        /// </summary>
        public string CustomizeId { get; }

        /// <summary>
        /// Gets the role ids (<c>st_role.sys_id</c>) the user holds in the company.
        /// </summary>
        public IReadOnlyList<string> Roles { get; }

        /// <summary>
        /// Gets the user's row id (<c>st_user.sys_rowid</c>), used for record-scope <c>Own</c> filtering.
        /// </summary>
        public Guid UserRowId { get; }

        /// <summary>
        /// Gets the user's linked employee row id (<c>st_employee.sys_rowid</c>), or
        /// <see cref="Guid.Empty"/> when the user has no employee in the company.
        /// </summary>
        public Guid EmployeeRowId { get; }

        /// <summary>
        /// Gets the employee's department row id (<c>st_employee.dept_rowid</c>), or
        /// <see cref="Guid.Empty"/> when there is none.
        /// </summary>
        public Guid DeptRowId { get; }
    }
}
