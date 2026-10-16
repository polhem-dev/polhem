using System.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;

namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract for the form operations a client calls for one program: list, lookup, new, load, save and delete.
    /// </summary>
    /// <remarks>
    /// A front end depends on this contract, not on a transport. The API client implements it over JSON-RPC; an
    /// application without a Polhem backend implements it over its own data. The members mirror the client's form
    /// connector, so code reads the same against either.
    /// </remarks>
    public interface IFormApiConnector
    {
        /// <summary>
        /// Gets the program identifier the connector serves.
        /// </summary>
        string ProgId { get; }

        /// <summary>
        /// Retrieves a list of the program's master rows.
        /// </summary>
        /// <param name="selectFields">The comma-separated fields to return; empty returns the default set.</param>
        /// <param name="filter">The filter to apply, or <c>null</c> for none.</param>
        /// <param name="sortFields">The sort order, or <c>null</c> for the default order.</param>
        /// <param name="paging">The page to return, or <c>null</c> for the default page size.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IGetListResponse> GetListAsync(string selectFields = "", FilterNode? filter = null,
            SortFieldCollection? sortFields = null, PagingOptions? paging = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the candidate rows a lookup picker shows for the program.
        /// </summary>
        /// <param name="searchText">The text to match; empty applies no search.</param>
        /// <param name="paging">The page to return, or <c>null</c> for the default page size.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IGetLookupResponse> GetLookupAsync(string searchText = "", PagingOptions? paging = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a blank record seeded with the program's defaults.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IGetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the master row identified by <paramref name="rowId"/> with its details.
        /// </summary>
        /// <param name="rowId">The master row identifier (<c>sys_rowid</c>).</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IGetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists the changes in <paramref name="dataSet"/>.
        /// </summary>
        /// <param name="dataSet">The record to persist; each row's state decides insert, update or delete.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<ISaveResponse> SaveAsync(DataSet dataSet, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the master row identified by <paramref name="rowId"/>.
        /// </summary>
        /// <param name="rowId">The master row identifier (<c>sys_rowid</c>).</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IDeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default);
    }
}
