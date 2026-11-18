using System.Data;
using Avalonia.DemoCenter.Modules.WithoutBackend;
using Polhem.Api.Contracts.Form;
using Polhem.Definition;
using Polhem.Definition.Forms;

namespace Avalonia.DemoCenter.Modules.QueryConditions
{
    /// <summary>
    /// An in-memory form connector that also implements <see cref="IUserQueryConnector"/>, so <c>ListView</c> offers
    /// saved queries at the top of its query pane. A backend keeps them per user in a table; here one list stands in
    /// for it, and a query marked as another user's cannot be changed, as the backend refuses it.
    /// </summary>
    internal sealed class InMemoryUserQueryConnector : InMemoryFormApiConnector, IUserQueryConnector
    {
        private readonly List<StoredQuery> _queries = [];
        private Guid _defaultRowId;

        public InMemoryUserQueryConnector(FormSchema schema, DataTable rows)
            : base(schema, rows)
        {
        }

        /// <summary>Stores a query, as a user would have saved it.</summary>
        /// <param name="name">The name.</param>
        /// <param name="criteria">The criteria text, written by <c>UserQueryCodec.Serialize</c>.</param>
        /// <param name="isShared">Whether every user may apply it.</param>
        /// <param name="isOwn">Whether the signed-in user created it, and so may change it.</param>
        /// <param name="isDefault">Whether the list opens with it applied.</param>
        public void Seed(string name, string criteria, bool isShared, bool isOwn, bool isDefault = false)
        {
            var query = new StoredQuery(Guid.NewGuid(), name, isShared, isOwn, criteria);
            _queries.Add(query);
            if (isDefault) _defaultRowId = query.RowId;
        }

        public Task<IGetUserQueriesResponse> GetUserQueriesAsync(CancellationToken cancellationToken = default)
        {
            // The columns the backend returns; the user's own queries come first.
            var table = new DataTable("UserQuery");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.Name, typeof(string));
            table.Columns.Add("is_shared", typeof(bool));
            table.Columns.Add("is_own", typeof(bool));
            table.Columns.Add("criteria", typeof(string));
            foreach (var query in _queries.OrderByDescending(q => q.IsOwn).ThenBy(q => q.Name, StringComparer.CurrentCulture))
                table.Rows.Add(query.RowId, query.Name, query.IsShared, query.IsOwn, query.Criteria);
            return Task.FromResult<IGetUserQueriesResponse>(new QueryList(table, _defaultRowId));
        }

        public Task<ISaveUserQueryResponse> SaveUserQueryAsync(Guid rowId, string name, bool isShared, string criteria,
            CancellationToken cancellationToken = default)
        {
            if (rowId == Guid.Empty)
            {
                rowId = Guid.NewGuid();
            }
            else
            {
                var index = OwnIndex(rowId);
                _queries.RemoveAt(index);
            }
            _queries.Add(new StoredQuery(rowId, name, isShared, true, criteria));
            return Task.FromResult<ISaveUserQueryResponse>(new Saved(rowId));
        }

        public Task<IDeleteUserQueryResponse> DeleteUserQueryAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            _queries.RemoveAt(OwnIndex(rowId));
            if (_defaultRowId == rowId) _defaultRowId = Guid.Empty;
            return Task.FromResult<IDeleteUserQueryResponse>(new Deleted(1));
        }

        public Task<ISetDefaultUserQueryResponse> SetDefaultUserQueryAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            _defaultRowId = rowId;
            return Task.FromResult<ISetDefaultUserQueryResponse>(new DefaultSet(rowId));
        }

        private int OwnIndex(Guid rowId)
        {
            var index = _queries.FindIndex(q => q.RowId == rowId);
            if (index < 0 || !_queries[index].IsOwn)
                throw new InvalidOperationException("Only a query the user created can be changed.");
            return index;
        }

        private sealed record StoredQuery(Guid RowId, string Name, bool IsShared, bool IsOwn, string Criteria);

        private sealed record QueryList(DataTable? Table, Guid DefaultRowId) : IGetUserQueriesResponse;

        private sealed record Saved(Guid RowId) : ISaveUserQueryResponse;

        private sealed record Deleted(int RowsAffected) : IDeleteUserQueryResponse;

        private sealed record DefaultSet(Guid DefaultRowId) : ISetDefaultUserQueryResponse;
    }
}
