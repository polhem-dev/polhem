using System.Data;

namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the GetUserQueries response.
    /// </summary>
    /// <remarks>
    /// The columns of <see cref="Table"/> are <c>sys_rowid</c> (Guid), <c>sys_name</c> (string), <c>is_shared</c> (bool), <c>is_own</c> (bool, whether the caller created it) and <c>criteria</c> (string, the stored criteria text). Own queries come first, each group ordered by name.
    /// </remarks>
    public interface IGetUserQueriesResponse
    {
        /// <summary>
        /// Gets the user queries the caller can apply on the form: their own and the ones other users share.
        /// </summary>
        DataTable? Table { get; }

        /// <summary>
        /// Gets the <c>sys_rowid</c> of the query the caller applies when the list opens, or <see cref="Guid.Empty"/> when there is none.
        /// </summary>
        Guid DefaultRowId { get; }
    }
}
