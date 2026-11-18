namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract for the user queries of a form: the query criteria a user saves under a name, personal or shared with
    /// the company, and the one the user applies when the list opens.
    /// </summary>
    /// <remarks>
    /// It is separate from <see cref="IFormApiConnector"/> so that adding it breaks no existing implementation: a
    /// front end checks whether its connector also implements this interface, and without it offers no saved queries.
    /// The API client's form connector implements both. The criteria text is written and read by the front end; the
    /// server stores it as given.
    /// </remarks>
    public interface IUserQueryConnector
    {
        /// <summary>
        /// Lists the user queries the caller can apply on the form, and the caller's default.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IGetUserQueriesResponse> GetUserQueriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a new user query, or overwrites one the caller created.
        /// </summary>
        /// <param name="rowId">The query to overwrite, or <see cref="Guid.Empty"/> to save a new one.</param>
        /// <param name="name">The name the query is listed under.</param>
        /// <param name="isShared">Whether every user of the company who can read the form may apply it.</param>
        /// <param name="criteria">The criteria text.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<ISaveUserQueryResponse> SaveUserQueryAsync(Guid rowId, string name, bool isShared, string criteria,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a user query the caller created.
        /// </summary>
        /// <param name="rowId">The query.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IDeleteUserQueryResponse> DeleteUserQueryAsync(Guid rowId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the user query the caller applies when the form's list opens.
        /// </summary>
        /// <param name="rowId">The query, or <see cref="Guid.Empty"/> to apply none.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<ISetDefaultUserQueryResponse> SetDefaultUserQueryAsync(Guid rowId, CancellationToken cancellationToken = default);
    }
}
