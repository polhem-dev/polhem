using System.Data;
using Polhem.Api.Contracts.AuditLog;

namespace Polhem.Business.AuditLog
{
    /// <summary>
    /// Shared output result for the audit-log aggregate queries (anomaly summary / top-N): a bounded,
    /// unpaged summary result set.
    /// </summary>
    public sealed class AuditLogAggregateResult : BusinessResult, IAuditLogAggregateResponse
    {
        /// <summary>Gets or sets the aggregate result rows.</summary>
        public DataTable? Table { get; set; }
    }
}
