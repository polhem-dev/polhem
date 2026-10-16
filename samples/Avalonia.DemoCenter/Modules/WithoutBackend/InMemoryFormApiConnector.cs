using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.UI.Core;
using Polhem.UI.Core.Connectors;

namespace Avalonia.DemoCenter.Modules.WithoutBackend
{
    /// <summary>
    /// An <see cref="IFormApiConnector"/> over one in-memory master table: what an application without a Polhem
    /// backend writes to put <c>FormView</c> and <c>ListView</c> on its own data (a REST API, a local database).
    /// </summary>
    /// <remarks>
    /// Master-only for brevity. It ignores the filter, sort and paging arguments and returns every row.
    /// </remarks>
    internal sealed class InMemoryFormApiConnector : IFormApiConnector
    {
        private readonly FormSchema _schema;
        private readonly DataTable _rows;

        public InMemoryFormApiConnector(FormSchema schema, DataTable rows)
        {
            _schema = schema;
            _rows = rows;
        }

        public string ProgId => _schema.ProgId;

        public Task<IGetListResponse> GetListAsync(string selectFields = "", FilterNode? filter = null,
            SortFieldCollection? sortFields = null, PagingOptions? paging = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IGetListResponse>(new FormListResult(_rows.Copy()));

        public Task<IGetLookupResponse> GetLookupAsync(string searchText = "", PagingOptions? paging = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IGetLookupResponse>(new FormListResult(_rows.Copy()));

        public Task<IGetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
        {
            var dataSet = FormValueBinding.BuildEmptyDataSet(_schema);
            var master = dataSet.Tables[_schema.ProgId]!;
            var row = master.NewRow();
            // The same schema-driven defaults the server applies to a new record, sys_rowid included.
            FormRowDefaults.Apply(_schema.MasterTable!, row);
            master.Rows.Add(row);
            return Task.FromResult<IGetNewDataResponse>(new FormDataResult(dataSet));
        }

        public Task<IGetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            var source = Find(rowId);
            if (source is null) return Task.FromResult<IGetDataResponse>(new FormDataResult(null));

            var dataSet = FormValueBinding.BuildEmptyDataSet(_schema);
            var master = dataSet.Tables[_schema.ProgId]!;
            master.ImportRow(source);
            master.AcceptChanges();
            return Task.FromResult<IGetDataResponse>(new FormDataResult(dataSet));
        }

        public Task<ISaveResponse> SaveAsync(DataSet dataSet, CancellationToken cancellationToken = default)
        {
            var edited = dataSet.Tables[_schema.ProgId]!.Rows[0];
            var rowId = (Guid)edited[SysFields.RowId];
            var target = Find(rowId);
            if (target is null)
            {
                _rows.ImportRow(edited);
            }
            else
            {
                foreach (DataColumn column in _rows.Columns)
                    target[column.ColumnName] = edited[column.ColumnName];
            }
            _rows.AcceptChanges();
            dataSet.AcceptChanges();
            return Task.FromResult<ISaveResponse>(new FormDataResult(dataSet));
        }

        public Task<IDeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
        {
            var target = Find(rowId);
            target?.Delete();
            _rows.AcceptChanges();
            return Task.FromResult<IDeleteResponse>(new FormDeleteResult(target is null ? 0 : 1));
        }

        private DataRow? Find(Guid rowId)
            => _rows.Rows.Cast<DataRow>().FirstOrDefault(row => row[SysFields.RowId] is Guid id && id == rowId);
    }
}
