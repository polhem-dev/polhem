using Polhem.Api.Contracts.AuditLog;

namespace Polhem.Business.AuditLog
{
    /// <summary>
    /// Input arguments for the DB-anomaly summary query.
    /// </summary>
    public sealed class GetDbAnomalySummaryArgs : BusinessArgs, IGetDbAnomalySummaryRequest
    {
        /// <summary>Gets or sets the inclusive lower bound on the event time (UTC).</summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>Gets or sets the inclusive upper bound on the event time (UTC).</summary>
        public DateTime? ToUtc { get; set; }
    }
}
