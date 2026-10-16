using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Definition.Paging;

namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// A page of rows returned by an <see cref="IFormApiConnector"/> implemented without a Polhem backend.
    /// </summary>
    /// <param name="Table">The rows.</param>
    /// <param name="Paging">The paging metadata, or <c>null</c> when the rows are not paged.</param>
    public sealed record FormListResult(DataTable? Table, PagingInfo? Paging = null) : IGetListResponse, IGetLookupResponse;
}
