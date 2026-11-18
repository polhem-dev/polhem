namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// A saved user query as the query pane lists it, read from
    /// <see cref="Polhem.Api.Contracts.Form.IUserQueryConnector.GetUserQueriesAsync"/>.
    /// </summary>
    public sealed class UserQueryEntry
    {
        /// <summary>Initializes a new instance of <see cref="UserQueryEntry"/>.</summary>
        /// <param name="rowId">The query's <c>sys_rowid</c>.</param>
        /// <param name="name">The name it is listed under.</param>
        /// <param name="isShared">Whether every user of the company may apply it.</param>
        /// <param name="isOwn">Whether the signed-in user created it, and so may change it.</param>
        /// <param name="criteria">The stored criteria text.</param>
        public UserQueryEntry(Guid rowId, string name, bool isShared, bool isOwn, string criteria)
        {
            RowId = rowId;
            Name = name ?? string.Empty;
            IsShared = isShared;
            IsOwn = isOwn;
            Criteria = criteria ?? string.Empty;
        }

        /// <summary>Gets the query's <c>sys_rowid</c>.</summary>
        public Guid RowId { get; }

        /// <summary>Gets the name the query is listed under.</summary>
        public string Name { get; }

        /// <summary>Gets a value indicating whether every user of the company may apply the query.</summary>
        public bool IsShared { get; }

        /// <summary>Gets a value indicating whether the signed-in user created the query, and so may change it.</summary>
        public bool IsOwn { get; }

        /// <summary>Gets the stored criteria text, which <see cref="Polhem.UI.Core.Criteria.UserQueryCodec"/> reads.</summary>
        public string Criteria { get; }
    }
}
