using Polhem.Api.Contracts.Form;
using Polhem.Definition.Paging;

namespace Polhem.Api.Core.Messages.Form
{
    /// <summary>
    /// API request for the form GetLookup operation.
    /// </summary>
    public sealed class GetLookupRequest : ApiRequest, IGetLookupRequest
    {
        /// <summary>
        /// Gets or sets the search text matched against the string-typed lookup fields;
        /// an empty value applies no search filter.
        /// </summary>
        public string SearchText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the paging options; <c>null</c> applies the server-side default
        /// page size.
        /// </summary>
        public PagingOptions? Paging { get; set; }
    }
}
