namespace Polhem.Repository.Abstractions.Form
{
    /// <summary>
    /// Data access for user queries (<c>st_user_query</c>) and each user's default one (<c>st_user_query_default</c>),
    /// in the company database of the caller's session.
    /// </summary>
    /// <remarks>
    /// The repository stores and reads; who may change which query is decided by the business object.
    /// </remarks>
    public interface IUserQueryRepository
    {
        /// <summary>
        /// Reads the queries a user can apply on a form, their own first, and the user's default.
        /// </summary>
        /// <param name="progId">The form.</param>
        /// <param name="userId">The user.</param>
        /// <returns>
        /// The queries and the default; both empty when the tables do not exist, so a deployment that has not created
        /// them yet still opens its lists.
        /// </returns>
        UserQueryList GetQueries(string progId, string userId);

        /// <summary>
        /// Reads who owns a query, or <c>null</c> when it does not exist in the session's company.
        /// </summary>
        /// <param name="rowId">The query's <c>sys_rowid</c>.</param>
        UserQueryOwner? GetOwner(Guid rowId);

        /// <summary>
        /// Inserts a query.
        /// </summary>
        /// <param name="rowId">The new query's <c>sys_rowid</c>.</param>
        /// <param name="progId">The form.</param>
        /// <param name="userId">The user who creates it.</param>
        /// <param name="name">The name.</param>
        /// <param name="isShared">Whether every user of the company may apply it.</param>
        /// <param name="criteria">The stored criteria text.</param>
        void Insert(Guid rowId, string progId, string userId, string name, bool isShared, string criteria);

        /// <summary>
        /// Overwrites the name, the sharing and the criteria of a query.
        /// </summary>
        /// <param name="rowId">The query's <c>sys_rowid</c>.</param>
        /// <param name="name">The name.</param>
        /// <param name="isShared">Whether every user of the company may apply it.</param>
        /// <param name="criteria">The stored criteria text.</param>
        /// <returns>The number of queries changed.</returns>
        int Update(Guid rowId, string name, bool isShared, string criteria);

        /// <summary>
        /// Deletes a query, and every user's default that names it, in one transaction.
        /// </summary>
        /// <param name="rowId">The query's <c>sys_rowid</c>.</param>
        /// <returns>The number of queries deleted.</returns>
        int Delete(Guid rowId);

        /// <summary>
        /// Replaces a user's default query on a form.
        /// </summary>
        /// <param name="progId">The form.</param>
        /// <param name="userId">The user.</param>
        /// <param name="rowId">The query's <c>sys_rowid</c>, or <see cref="Guid.Empty"/> to clear the default.</param>
        void SetDefault(string progId, string userId, Guid rowId);
    }
}
