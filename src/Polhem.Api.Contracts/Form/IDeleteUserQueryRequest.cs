namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the DeleteUserQuery request: deletes a user query the caller created.
    /// </summary>
    /// <remarks>
    /// Only the user who created a query may delete it. Deleting it also clears it as any user's default.
    /// </remarks>
    public interface IDeleteUserQueryRequest
    {
        /// <summary>
        /// Gets the <c>sys_rowid</c> of the query to delete.
        /// </summary>
        Guid RowId { get; }
    }
}
