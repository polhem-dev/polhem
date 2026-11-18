namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the SaveUserQuery request: saves a new user query, or overwrites one the caller created.
    /// </summary>
    /// <remarks>
    /// Only the user who created a query may overwrite it, rename it or change whether it is shared.
    /// </remarks>
    public interface ISaveUserQueryRequest
    {
        /// <summary>
        /// Gets the <c>sys_rowid</c> of the query to overwrite, or <see cref="Guid.Empty"/> to save a new one.
        /// </summary>
        Guid RowId { get; }

        /// <summary>
        /// Gets the name the query is listed under.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a value indicating whether every user of the company who can read the form may apply the query.
        /// </summary>
        bool IsShared { get; }

        /// <summary>
        /// Gets the stored criteria text, written by the front end; the server does not read it.
        /// </summary>
        string Criteria { get; }
    }
}
