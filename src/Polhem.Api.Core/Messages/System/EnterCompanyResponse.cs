using Polhem.Api.Contracts.System;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Api.Core.Messages.System
{
    /// <summary>
    /// API response for the EnterCompany operation.
    /// </summary>
    public sealed class EnterCompanyResponse : ApiResponse, IEnterCompanyResponse
    {
        /// <summary>
        /// Gets or sets the company information that was bound to the session.
        /// </summary>
        public CompanyInfo Company { get; set; } = new CompanyInfo();

        /// <summary>
        /// Gets or sets the per-model allowed action mask (capability snapshot) for the session's
        /// roles in the entered company. The client caches this on <c>ClientInfo</c> and its element
        /// capability resolver reads it to degrade toolbar commands, grid actions, and sensitive
        /// fields. A model absent from the map means no permission.
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
