using Polhem.Api.Contracts.System;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Business.System
{
    /// <summary>
    /// Output result for the EnterCompany operation.
    /// </summary>
    public sealed class EnterCompanyResult : BusinessResult, IEnterCompanyResponse
    {
        /// <summary>
        /// Gets or sets the company information that was bound to the session.
        /// </summary>
        public CompanyInfo Company { get; set; } = new CompanyInfo();

        /// <summary>
        /// Gets or sets the per-model allowed action mask (capability snapshot) for the session's
        /// roles in the entered company. Copied to the wire response by <c>ApiOutputConverter</c>.
        /// </summary>
        public Dictionary<string, PermissionActions> Capabilities { get; set; } = [];

        /// <summary>
        /// Gets or sets the signed-in user's row id (<c>st_user.sys_rowid</c>).
        /// </summary>
        public Guid UserRowId { get; set; }

        /// <summary>
        /// Gets or sets the row id of the employee linked to the signed-in user in the entered company;
        /// <see cref="Guid.Empty"/> when the user has none there.
        /// </summary>
        public Guid EmployeeRowId { get; set; }

        /// <inheritdoc />
        IReadOnlyDictionary<string, PermissionActions> IEnterCompanyResponse.Capabilities => Capabilities;
    }
}
