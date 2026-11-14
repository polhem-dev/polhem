using System.Data;
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Transformers;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Sorting;
using Polhem.JsonRpc.Payload;

namespace Polhem.Web.Blazor.Wasm.BrowserTests
{
    /// <summary>
    /// The calls the WebAssembly wire test makes, run by the page in the browser and by the test on the desktop runtime,
    /// so the two reports can be compared step by step.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: <c>Polhem.Web.Blazor.Wasm.WireApp</c> compiles this file too, like <see cref="WireSample"/>. A step that
    /// throws is reported with its exception type and the scenario goes on, so one failure does not hide the others.
    /// </remarks>
    public static class WireScenario
    {
        /// <summary>Runs the scenario against the backend <paramref name="client"/> calls.</summary>
        /// <param name="client">A client of the backend, not signed in.</param>
        /// <param name="scenario">One of <see cref="WireSample.Scenarios"/>.</param>
        /// <param name="runtime">
        /// Names the runtime that runs the scenario. It starts the <c>sys_id</c> of every row saved, so each run reads only
        /// its own rows.
        /// </param>
        /// <returns>The report, one entry per step.</returns>
        public static async Task<List<WireStep>> RunAsync(PolhemApiClient client, string scenario, string runtime)
        {
            ArgumentNullException.ThrowIfNull(client);
            var prefix = $"{runtime}-{scenario}-";
            var steps = new List<WireStep>();

            if (!await RunStepAsync(steps, "sign in", async values =>
            {
                await SignInAsync(client).ConfigureAwait(false);
                values["signed in"] = WireSample.Format(client.Session.Credentials.AccessToken != Guid.Empty);
            }).ConfigureAwait(false))
            {
                return steps;
            }

            var form = client.Form(WireSample.ProgId);
            form.PayloadCodec = scenario == WireSample.Json ? PayloadCodecNames.Json : string.Empty;

            foreach (var row in WireSample.Rows)
                await SaveAndReadBackAsync(steps, form, prefix, row).ConfigureAwait(false);

            foreach (var (name, filter, _) in WireSample.Filters)
            {
                await RunStepAsync(steps, $"list {name}", async values =>
                {
                    var group = new FilterGroup(LogicalOperator.And);
                    group.Nodes.Add(FilterCondition.StartsWith(SysFields.Id, prefix));
                    group.Nodes.Add(filter());
                    var request = new GetListRequest
                    {
                        SelectFields = SysFields.Id,
                        Filter = group,
                        SortFields = [new SortField(SysFields.Id, SortDirection.Asc)],
                    };
                    var format = scenario == WireSample.Plain ? PayloadFormat.Plain : PayloadFormat.Encrypted;
                    var response = await form.ExecuteAsync<GetListResponse>(FormActions.GetList, request, format)
                        .ConfigureAwait(false);
                    var ids = response.Table!.Rows.Cast<DataRow>()
                        .Select(r => Convert.ToString(r[SysFields.Id], System.Globalization.CultureInfo.InvariantCulture)!)
                        .Select(id => id[prefix.Length..])
                        .OrderBy(id => id, StringComparer.Ordinal);
                    values["rows"] = string.Join(",", ids);
                }).ConfigureAwait(false);
            }

            return steps;
        }

        /// <summary>Signs <paramref name="client"/> in with the sample account and enters the sample company.</summary>
        /// <param name="client">A client of the backend.</param>
        public static async Task SignInAsync(PolhemApiClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            await client.System.LoginAsync(WireSample.UserId, WireSample.Password).ConfigureAwait(false);
            await client.System.EnterCompanyAsync(WireSample.CompanyId).ConfigureAwait(false);
        }

        /// <summary>Reads a saved master row back the way the page does, formatting every value it holds.</summary>
        /// <param name="form">The connector of the sample form.</param>
        /// <param name="rowId">The row's <c>sys_rowid</c>.</param>
        /// <returns>The values by field name, the detail lines under <c>lines</c>.</returns>
        public static async Task<SortedDictionary<string, string>> ReadBackAsync(FormApiConnector form, Guid rowId)
        {
            ArgumentNullException.ThrowIfNull(form);
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var dataSet = (await form.GetDataAsync(rowId).ConfigureAwait(false)).DataSet!;
            var master = dataSet.Tables[WireSample.ProgId]!.Rows[0];
            foreach (var (name, _) in WireSample.Fields)
                values[name] = WireSample.Format(master[name]);
            // The server's own time, held in the user's time zone. Another client reading the same row must see the same
            // value; against the clock only how recent it is can be checked.
            values[WireSample.ServerTimeRecent] = master[WireSample.ServerTimeField] is DateTime stored
                && Math.Abs((stored - DateTime.UtcNow).TotalDays) < 2
                ? $"datetime:{stored.Kind}:recent"
                : WireSample.Format(master[WireSample.ServerTimeField]);
            values[WireSample.AutoIncrementField] = master[WireSample.AutoIncrementField] is int number && number > 0
                ? "assigned" : WireSample.Format(master[WireSample.AutoIncrementField]);
            var lines = dataSet.Tables[WireSample.LineTable]!.Rows.Cast<DataRow>()
                .OrderBy(line => (int)line["line_no"])
                .Select(line => $"{WireSample.Format(line["line_no"])}|{WireSample.Format(line["qty"])}|{WireSample.Format(line["note"])}");
            values["lines"] = string.Join(";", lines);
            return values;
        }

        private static async Task SaveAndReadBackAsync(List<WireStep> steps, FormApiConnector form, string prefix, string row)
        {
            var rowId = Guid.Empty;
            var sent = WireSample.Values(row);

            await RunStepAsync(steps, $"save {row}", async values =>
            {
                var dataSet = (await form.GetNewDataAsync().ConfigureAwait(false)).DataSet!;
                var master = dataSet.Tables[WireSample.ProgId]!.Rows[0];
                master[SysFields.Id] = prefix + row;
                master[SysFields.Name] = $"Wire {row}";
                foreach (var (name, value) in sent)
                    master[name] = value;
                rowId = (Guid)master[SysFields.RowId];

                var lines = dataSet.Tables[WireSample.LineTable]!;
                for (int i = 1; i <= 2; i++)
                {
                    var line = lines.NewRow();
                    line[SysFields.RowId] = Guid.NewGuid();
                    line[SysFields.MasterRowId] = rowId;
                    line["line_no"] = i;
                    line["qty"] = 1.5m * i;
                    line["note"] = $"{row}{i}";
                    lines.Rows.Add(line);
                }

                await form.SaveAsync(dataSet).ConfigureAwait(false);
                foreach (var (name, value) in sent)
                    values[name] = WireSample.Format(value);
            }).ConfigureAwait(false);

            await RunStepAsync(steps, $"read back {row}", async values =>
            {
                foreach (var (name, value) in await ReadBackAsync(form, rowId).ConfigureAwait(false))
                    values[name] = value;
            }).ConfigureAwait(false);
        }

        private static async Task<bool> RunStepAsync(List<WireStep> steps, string name, Func<SortedDictionary<string, string>, Task> body)
        {
            var step = new WireStep { Name = name };
            steps.Add(step);
            try
            {
                await body(step.Values).ConfigureAwait(false);
                step.Ok = true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Boundary: a failed step is the finding; the report names it and the scenario continues.
                step.Error = $"{ex.GetType().FullName}: {ex.Message}";
            }
            return step.Ok;
        }
    }
}
