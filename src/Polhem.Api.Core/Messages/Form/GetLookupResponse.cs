using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Paging;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API response for the form GetLookup operation.
    /// </summary>
    public class GetLookupResponse : ApiResponse, IGetLookupResponse
    {
        /// <summary>
        /// Gets or sets the lookup candidate rows.
        /// </summary>
        public DataTable? Table { get; set; }

        /// <summary>
        /// Gets or sets the paging metadata; <c>null</c> when the query was unpaged.
        /// </summary>
        public PagingInfo? Paging { get; set; }
    }
}
