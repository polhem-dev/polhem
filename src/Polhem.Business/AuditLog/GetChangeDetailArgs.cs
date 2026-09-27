using Polhem.Api.Contracts.AuditLog;

namespace Polhem.Business.AuditLog
{
    /// <summary>
    /// Input arguments for retrieving one change event's restored before/after detail.
    /// </summary>
    public sealed class GetChangeDetailArgs : BusinessArgs, IGetChangeDetailRequest
    {
        /// <summary>
        /// Gets or sets the change event's log row id (<c>st_log_change.sys_rowid</c>).
        /// </summary>
        public Guid SysRowId { get; set; }
    }
}
