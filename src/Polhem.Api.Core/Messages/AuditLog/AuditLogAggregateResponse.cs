using System.Data;
using Polhem.Api.Contracts.AuditLog;

namespace Polhem.Api.Core.Messages.AuditLog
{
    /// <summary>
    /// Shared API response for the audit-log aggregate operations (anomaly summary / top-N): a bounded,
    /// unpaged summary result set. The <see cref="Table"/> carries whichever dimension / metric columns
    /// the aggregate projects.
    /// </summary>
    public sealed class AuditLogAggregateResponse : ApiResponse, IAuditLogAggregateResponse
    {
        /// <summary>Gets or sets the aggregate result rows.</summary>
        public DataTable? Table { get; set; }

        // Add new fields starting from Key(101).
    }
}
