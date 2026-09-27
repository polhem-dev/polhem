using Polhem.Api.Contracts.AuditLog;
using System.Text.Json.Serialization;

namespace Polhem.Api.Core.Messages.AuditLog
{
    /// <summary>
    /// API request for the top-API-methods operation.
    /// </summary>
    public sealed class GetTopApiMethodsRequest : ApiRequest, IGetTopApiMethodsRequest
    {
        /// <summary>Gets or sets the inclusive lower bound on the event time (UTC).</summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>Gets or sets the inclusive upper bound on the event time (UTC).</summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>Gets or sets how many top methods to return; the server clamps it to a sane range.</summary>
        // Written even when 0/default: the initialiser is not the CLR default, so omitting the value would let the
        // reader substitute the initialiser. Enforced for every wire member by `WireDefaultOmissionTests`.
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int TopN { get; set; } = 10;

        // Add new fields starting from Key(103).
    }
}
