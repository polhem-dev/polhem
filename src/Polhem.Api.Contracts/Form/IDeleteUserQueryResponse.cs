namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the DeleteUserQuery response.
    /// </summary>
    public interface IDeleteUserQueryResponse
    {
        /// <summary>
        /// Gets the number of queries deleted: zero when the query no longer exists.
        /// </summary>
        int RowsAffected { get; }
    }
}
