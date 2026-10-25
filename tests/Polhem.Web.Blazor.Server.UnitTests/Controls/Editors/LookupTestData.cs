using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Connectors;
using Polhem.UI.Core.Definitions;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls.Editors
{
    /// <summary>
    /// A lookup target (<c>Dept</c>), a form that looks it up (<c>Employee.dept_rowid</c>), and a connector
    /// that serves the target's rows, shared by the lookup tests.
    /// </summary>
    internal static class LookupTestData
    {
        public const string LookupField = "dept_rowid";
        public static readonly Guid EngineeringId = new("11111111-1111-1111-1111-111111111111");
        public static readonly Guid SalesId = new("22222222-2222-2222-2222-222222222222");

        public static FormSchema BuildTargetSchema()
        {
            var schema = new FormSchema("Dept", "Department");
            var fields = schema.Tables!.Add("Dept", "Department").Fields!;
            fields.Add("sys_rowid", "Row", FieldDbType.Guid);
            fields.Add("sys_id", "Code", FieldDbType.String);
            fields.Add("sys_name", "Name", FieldDbType.String);
            return schema;
        }

        public static FormDataObject BuildEmployee(bool withMappings = true)
        {
            var schema = new FormSchema("Employee", "Employee");
            var fields = schema.Tables!.Add("Employee", "Employee").Fields!;
            var dept = fields.Add(LookupField, "Department", FieldDbType.Guid);
            dept.RelationProgId = "Dept";
            if (withMappings)
            {
                dept.RelationFieldMappings!.Add("sys_id", "ref_dept_id");
                dept.RelationFieldMappings.Add("sys_name", "ref_dept_name");
            }
            fields.Add("ref_dept_id", "Department Code", FieldDbType.String);
            fields.Add("ref_dept_name", "Department Name", FieldDbType.String);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            return dataObject;
        }

        public static DataTable BuildTargetRows()
        {
            var table = new DataTable("Dept");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Rows.Add(EngineeringId, "D01", "Engineering");
            table.Rows.Add(SalesId, "D02", "Sales");
            return table;
        }
    }

    /// <summary>
    /// Supplies the lookup target's schema and connector the way a host without a Polhem backend registers them.
    /// </summary>
    internal sealed class FakeLookupSource : IFormDefinitionSource, IFormApiConnectorFactory
    {
        public FakeLookupConnector Connector { get; } = new();

        public IFormApiConnector Create(string progId) => Connector;

        public Task<FormSchema> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
            => Task.FromResult(LookupTestData.BuildTargetSchema());

        public Task<FormLayout> GetFormLayoutAsync(string progId, FormSchema schema, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CurrencySettings?> GetCurrencySettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<CurrencySettings?>(null);

        public Task<UnitSettings?> GetUnitSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<UnitSettings?>(null);
    }

    /// <summary>
    /// Serves the lookup rows, filtered by name, and records each search. Every other operation is
    /// outside what a lookup calls.
    /// </summary>
    internal sealed class FakeLookupConnector : IFormApiConnector
    {
        private readonly DataTable _rows = LookupTestData.BuildTargetRows();

        public string ProgId => "Dept";

        public List<string> Searches { get; } = [];

        public Exception? Failure { get; set; }

        public Task<IGetLookupResponse> GetLookupAsync(string searchText = "", PagingOptions? paging = null,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(searchText);
            if (Failure is not null) return Task.FromException<IGetLookupResponse>(Failure);
            var result = _rows.Clone();
            foreach (DataRow row in _rows.Rows)
            {
                if (((string)row["sys_name"]).Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    result.ImportRow(row);
            }
            return Task.FromResult<IGetLookupResponse>(new FormListResult(result));
        }

        public Task<IGetListResponse> GetListAsync(string selectFields = "", FilterNode? filter = null,
            SortFieldCollection? sortFields = null, PagingOptions? paging = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IGetNewDataResponse> GetNewDataAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IGetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ISaveResponse> SaveAsync(DataSet dataSet, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IDeleteResponse> DeleteAsync(Guid rowId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
