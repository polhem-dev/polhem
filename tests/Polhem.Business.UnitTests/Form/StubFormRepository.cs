using System.Data;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Identity;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;

namespace Polhem.Business.UnitTests.Form
{
    /// <summary>
    /// A form repository that touches no database: it answers the scope and stored-owner checks from what the test
    /// configures and records what reached it.
    /// </summary>
    internal sealed class StubFormRepository : IDataFormRepository
    {
        /// <summary>The master rowids the authoritative scope check confirms.</summary>
        public HashSet<Guid> InScope { get; } = [];

        /// <summary>The table returned for a stored-owner lookup; by default one row per configured owner.</summary>
        public Func<string, IReadOnlyCollection<Guid>, DataTable>? RowsByRowId { get; set; }

        /// <summary>The stored owner of each detail row, keyed by the detail's rowid.</summary>
        public Dictionary<Guid, Guid> StoredOwners { get; } = [];

        /// <summary>The data set <see cref="GetData"/> returns.</summary>
        public DataSet? Snapshot { get; set; }

        /// <summary>The row count <see cref="Delete"/> reports.</summary>
        public int DeleteCount { get; set; } = 1;

        public List<Guid> ScopeChecks { get; } = [];
        public List<DataSet> Saved { get; } = [];

        public bool ExistsInScope(Guid rowId, FilterNode? scopeFilter)
        {
            ScopeChecks.Add(rowId);
            return InScope.Contains(rowId);
        }

        public DataTable GetRowsByRowId(string tableName, string selectFields, IReadOnlyCollection<Guid> rowIds)
        {
            if (RowsByRowId != null) { return RowsByRowId(tableName, rowIds); }
            var table = new DataTable(tableName);
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            table.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            foreach (var rowId in rowIds.Where(StoredOwners.ContainsKey))
                table.Rows.Add(rowId, StoredOwners[rowId]);
            return table;
        }

        public (DataSet? Refreshed, Dictionary<string, int> AffectedRows) Save(DataSet dataSet)
        {
            Saved.Add(dataSet);
            return (null, []);
        }

        public DataFormListResult GetList(string selectFields, FilterNode? filter, SortFieldCollection? sortFields, PagingOptions? paging = null)
            => new() { Table = new DataTable() };
        public DataSet GetNewData(string timeZoneId = "") => new();
        public DataSet? GetData(Guid rowId, FilterNode? scopeFilter = null) => Snapshot;
        public int Delete(Guid rowId, FilterNode? scopeFilter = null) => DeleteCount;
    }

    /// <summary>A repository factory that hands out one form repository for every progId.</summary>
    internal sealed class StubRepositoryFactory : IRepositoryFactory
    {
        private readonly IDataFormRepository _repo;
        public StubRepositoryFactory(IDataFormRepository repo) { _repo = repo; }
        public T CreateFormRepository<T>(Guid accessToken, string progId, SessionCompanyScope companyScope) where T : class, IDataFormRepository => (T)_repo;
        public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository => (T)_repo;
        public T Create<T>(Guid accessToken = default) where T : class => throw new NotSupportedException();
    }
}
