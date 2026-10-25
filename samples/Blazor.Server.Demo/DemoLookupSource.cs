using System.Data;
using Polhem.Api.Contracts.Form;
using Polhem.Core.Data;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Paging;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.UI.Core.Connectors;
using Polhem.UI.Core.Definitions;

namespace Blazor.Server.Demo;

/// <summary>
/// Serves the in-memory <c>DemoTeam</c> program that the field editor page looks up, so the lookup works
/// without signing in.
/// </summary>
/// <remarks>
/// A lookup dialog takes its schema and connector from an <see cref="IFormDefinitionSource"/> and an
/// <see cref="IFormApiConnectorFactory"/> registered in the container before it falls back to the circuit's
/// <c>PolhemApiClient</c>. That is the extension point for an application without a Polhem backend. An
/// application with one registers neither, and its lookups query the backend. Because this source answers
/// every lookup in the demo, it serves only <c>DemoTeam</c> and rejects anything else.
/// </remarks>
internal sealed class DemoLookupSource : IFormDefinitionSource, IFormApiConnectorFactory
{
    public const string ProgId = "DemoTeam";

    public IFormApiConnector Create(string progId)
        => progId == ProgId ? new DemoTeamConnector() : throw Unknown(progId);

    public Task<FormSchema> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
    {
        if (progId != ProgId) throw Unknown(progId);
        var schema = new FormSchema(ProgId, "Team") { LookupFields = "sys_id,sys_name" };
        var fields = schema.Tables!.Add(ProgId, "Team").Fields!;
        fields.Add("sys_rowid", "Row", FieldDbType.Guid);
        fields.Add("sys_id", "Team ID", FieldDbType.String);
        fields.Add("sys_name", "Team Name", FieldDbType.String);
        return Task.FromResult(schema);
    }

    public Task<FormLayout> GetFormLayoutAsync(string progId, FormSchema schema, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The demo lookup source serves schemas only.");

    public Task<CurrencySettings?> GetCurrencySettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<CurrencySettings?>(null);

    public Task<UnitSettings?> GetUnitSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<UnitSettings?>(null);

    private static InvalidOperationException Unknown(string progId)
        => new($"The demo lookup source only serves '{ProgId}', not '{progId}'.");

    private sealed class DemoTeamConnector : IFormApiConnector
    {
        public string ProgId => DemoLookupSource.ProgId;

        public Task<IGetLookupResponse> GetLookupAsync(string searchText = "", PagingOptions? paging = null,
            CancellationToken cancellationToken = default)
        {
            var table = new DataTable(ProgId);
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            foreach (var (id, name, rowId) in s_teams)
            {
                if (name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
                    || id.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    table.Rows.Add(rowId, id, name);
            }
            return Task.FromResult<IGetLookupResponse>(new FormListResult(table));
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

        private static readonly (string Id, string Name, Guid RowId)[] s_teams =
        [
            ("T01", "Platform", new Guid("6f1d1d2e-0001-4c3a-9a51-000000000001")),
            ("T02", "Mobile", new Guid("6f1d1d2e-0002-4c3a-9a51-000000000002")),
            ("T03", "Customer Success", new Guid("6f1d1d2e-0003-4c3a-9a51-000000000003")),
        ];
    }
}
