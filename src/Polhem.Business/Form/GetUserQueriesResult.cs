using System.Data;

using Polhem.Api.Contracts.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Output result for the <c>GetUserQueries</c> operation.
    /// </summary>
    public sealed class GetUserQueriesResult : BusinessResult, IGetUserQueriesResponse
    {
        /// <summary>
        /// Gets or sets the user queries the caller can apply on the form: their own and the ones other users share.
        /// </summary>
        public DataTable? Table { get; set; }

        /// <summary>
        /// Gets or sets the <c>sys_rowid</c> of the query the caller applies when the list opens, or <see cref="Guid.Empty"/> when there is none.
        /// </summary>
        public Guid DefaultRowId { get; set; }
    }
}
