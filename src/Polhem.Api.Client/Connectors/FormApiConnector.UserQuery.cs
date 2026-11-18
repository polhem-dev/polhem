using Polhem.Api.Contracts.Form;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition;

namespace Polhem.Api.Client.Connectors
{
    /// <summary>
    /// User-query half of <see cref="FormApiConnector"/>.
    /// </summary>
    public partial class FormApiConnector
    {
        /// <summary>
        /// Asynchronously lists the user queries the caller can apply on the form, and the caller's default.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<GetUserQueriesResponse> GetUserQueriesAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<GetUserQueriesResponse>(FormActions.GetUserQueries, new GetUserQueriesRequest(),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously saves a new user query, or overwrites one the caller created.
        /// </summary>
        /// <param name="rowId">The query to overwrite, or <see cref="Guid.Empty"/> to save a new one.</param>
        /// <param name="name">The name the query is listed under.</param>
        /// <param name="isShared">Whether every user of the company who can read the form may apply it.</param>
        /// <param name="criteria">The criteria text.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<SaveUserQueryResponse> SaveUserQueryAsync(Guid rowId, string name, bool isShared,
            string criteria, CancellationToken cancellationToken = default)
        {
            var request = new SaveUserQueryRequest { RowId = rowId, Name = name, IsShared = isShared, Criteria = criteria };
            return await ExecuteAsync<SaveUserQueryResponse>(FormActions.SaveUserQuery, request,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously deletes a user query the caller created.
        /// </summary>
        /// <param name="rowId">The query.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<DeleteUserQueryResponse> DeleteUserQueryAsync(Guid rowId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<DeleteUserQueryResponse>(FormActions.DeleteUserQuery,
                    new DeleteUserQueryRequest { RowId = rowId }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously sets the user query the caller applies when the form's list opens.
        /// </summary>
        /// <param name="rowId">The query, or <see cref="Guid.Empty"/> to apply none.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        public virtual async Task<SetDefaultUserQueryResponse> SetDefaultUserQueryAsync(Guid rowId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<SetDefaultUserQueryResponse>(FormActions.SetDefaultUserQuery,
                    new SetDefaultUserQueryRequest { RowId = rowId }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        async Task<IGetUserQueriesResponse> IUserQueryConnector.GetUserQueriesAsync(CancellationToken cancellationToken)
            => await GetUserQueriesAsync(cancellationToken).ConfigureAwait(false);

        async Task<ISaveUserQueryResponse> IUserQueryConnector.SaveUserQueryAsync(Guid rowId, string name, bool isShared,
            string criteria, CancellationToken cancellationToken)
            => await SaveUserQueryAsync(rowId, name, isShared, criteria, cancellationToken).ConfigureAwait(false);

        async Task<IDeleteUserQueryResponse> IUserQueryConnector.DeleteUserQueryAsync(Guid rowId,
            CancellationToken cancellationToken)
            => await DeleteUserQueryAsync(rowId, cancellationToken).ConfigureAwait(false);

        async Task<ISetDefaultUserQueryResponse> IUserQueryConnector.SetDefaultUserQueryAsync(Guid rowId,
            CancellationToken cancellationToken)
            => await SetDefaultUserQueryAsync(rowId, cancellationToken).ConfigureAwait(false);
    }
}
