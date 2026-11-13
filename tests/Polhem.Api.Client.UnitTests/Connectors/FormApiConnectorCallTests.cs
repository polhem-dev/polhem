using System.ComponentModel;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Contracts.Form;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition;
using Polhem.Definition.Paging;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// The typed calls of <see cref="FormApiConnector"/>, through its public methods and through the
    /// <see cref="IFormApiConnector"/> and <see cref="IFieldValueChangeConnector"/> contracts. Each test pins the
    /// method sent and the request fields.
    /// </summary>
    public class FormApiConnectorCallTests
    {
        private const string ProgId = "Employee";

        private static (FormApiConnector Connector, FakeApiTransport Transport) Create(object resultValue)
        {
            var transport = new FakeApiTransport(call => FakeApiTransport.Answer(call, resultValue));
            return (new FormApiConnector(TestClients.Fake(transport), ProgId), transport);
        }

        private static DataSet NewDataSet()
        {
            var dataSet = new DataSet("Employee");
            var table = dataSet.Tables.Add("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Rows.Add("E001");
            return dataSet;
        }

        private static DataTable NewRow()
        {
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Rows.Add("E001");
            return table;
        }

        [Fact]
        [DisplayName("ExecFuncAsync sends ExecFunc to the form's progId with the function id")]
        public async Task ExecFuncAsync_SendsExecFuncWithFuncId()
        {
            var (connector, transport) = Create(new ExecFuncResponse());

            var response = await connector.ExecFuncAsync(new ExecFuncRequest("Recalc"));

            Assert.NotNull(response);
            Assert.Equal($"{ProgId}.{SystemActions.ExecFunc}", transport.LastCall!.Method);
            Assert.Equal("Recalc", transport.LastCall!.SentValue<ExecFuncRequest>().FuncId);
        }

        [Fact]
        [DisplayName("ExecFuncAnonymousAsync sends ExecFuncAnonymous to the form's progId")]
        public async Task ExecFuncAnonymousAsync_SendsAnonymousAction()
        {
            var (connector, transport) = Create(new ExecFuncResponse());

            var response = await connector.ExecFuncAnonymousAsync(new ExecFuncRequest("Ping"));

            Assert.NotNull(response);
            Assert.Equal($"{ProgId}.{SystemActions.ExecFuncAnonymous}", transport.LastCall!.Method);
        }

        [Fact]
        [DisplayName("GetListAsync sends GetList with the select fields and paging and returns the paging info")]
        public async Task GetListAsync_SendsRequestFields_ReturnsResponse()
        {
            var (connector, transport) = Create(new GetListResponse { Paging = new PagingInfo { TotalCount = 7 } });

            var response = await connector.GetListAsync("sys_id,sys_name", paging: new PagingOptions { Page = 2, PageSize = 5 });

            Assert.Equal($"{ProgId}.{FormActions.GetList}", transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<GetListRequest>();
            Assert.Equal("sys_id,sys_name", sent.SelectFields);
            Assert.Equal(2, sent.Paging!.Page);
            Assert.Equal(7, response.Paging!.TotalCount);
        }

        [Fact]
        [DisplayName("GetListAsync through IFormApiConnector forwards to the same GetList call")]
        public async Task GetListAsync_ThroughContract_Forwards()
        {
            var (connector, transport) = Create(new GetListResponse());

            var response = await ((IFormApiConnector)connector).GetListAsync("sys_id", null, null, null, CancellationToken.None);

            Assert.IsType<GetListResponse>(response);
            Assert.Equal($"{ProgId}.{FormActions.GetList}", transport.LastCall!.Method);
        }

        [Fact]
        [DisplayName("GetLookupAsync sends GetLookup with the search text and paging")]
        public async Task GetLookupAsync_SendsSearchTextAndPaging()
        {
            var (connector, transport) = Create(new GetLookupResponse());

            await connector.GetLookupAsync("ann", new PagingOptions { Page = 1, PageSize = 10 });

            Assert.Equal($"{ProgId}.{FormActions.GetLookup}", transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<GetLookupRequest>();
            Assert.Equal("ann", sent.SearchText);
            Assert.Equal(10, sent.Paging!.PageSize);
        }

        [Fact]
        [DisplayName("GetLookupAsync through IFormApiConnector forwards to the same GetLookup call")]
        public async Task GetLookupAsync_ThroughContract_Forwards()
        {
            var (connector, transport) = Create(new GetLookupResponse());

            var response = await ((IFormApiConnector)connector).GetLookupAsync("x", null, CancellationToken.None);

            Assert.IsType<GetLookupResponse>(response);
            Assert.Equal($"{ProgId}.{FormActions.GetLookup}", transport.LastCall!.Method);
        }

        [Fact]
        [DisplayName("GetNewDataAsync sends GetNewData and returns the blank DataSet")]
        public async Task GetNewDataAsync_SendsAction_ReturnsDataSet()
        {
            var (connector, transport) = Create(new GetNewDataResponse { DataSet = NewDataSet() });

            var response = await connector.GetNewDataAsync();

            Assert.Equal($"{ProgId}.{FormActions.GetNewData}", transport.LastCall!.Method);
            Assert.Equal("E001", response.DataSet!.Tables["Employee"]!.Rows[0]["sys_id"]);
        }

        [Fact]
        [DisplayName("GetNewDataAsync through IFormApiConnector forwards to the same GetNewData call")]
        public async Task GetNewDataAsync_ThroughContract_Forwards()
        {
            var (connector, transport) = Create(new GetNewDataResponse());

            var response = await ((IFormApiConnector)connector).GetNewDataAsync(CancellationToken.None);

            Assert.IsType<GetNewDataResponse>(response);
            Assert.Equal($"{ProgId}.{FormActions.GetNewData}", transport.LastCall!.Method);
        }

        [Fact]
        [DisplayName("GetDataAsync sends GetData with the row id")]
        public async Task GetDataAsync_SendsRowId()
        {
            var rowId = Guid.NewGuid();
            var (connector, transport) = Create(new GetDataResponse());

            await connector.GetDataAsync(rowId);

            Assert.Equal($"{ProgId}.{FormActions.GetData}", transport.LastCall!.Method);
            Assert.Equal(rowId, transport.LastCall!.SentValue<GetDataRequest>().RowId);
        }

        [Fact]
        [DisplayName("GetDataAsync through IFormApiConnector forwards to the same GetData call")]
        public async Task GetDataAsync_ThroughContract_Forwards()
        {
            var rowId = Guid.NewGuid();
            var (connector, transport) = Create(new GetDataResponse());

            var response = await ((IFormApiConnector)connector).GetDataAsync(rowId, CancellationToken.None);

            Assert.IsType<GetDataResponse>(response);
            Assert.Equal(rowId, transport.LastCall!.SentValue<GetDataRequest>().RowId);
        }

        [Fact]
        [DisplayName("SaveAsync sends Save with the DataSet")]
        public async Task SaveAsync_SendsDataSet()
        {
            var (connector, transport) = Create(new SaveResponse());

            await connector.SaveAsync(NewDataSet());

            Assert.Equal($"{ProgId}.{FormActions.Save}", transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<SaveRequest>();
            Assert.Equal("E001", sent.DataSet!.Tables["Employee"]!.Rows[0]["sys_id"]);
        }

        [Fact]
        [DisplayName("SaveAsync through IFormApiConnector forwards to the same Save call")]
        public async Task SaveAsync_ThroughContract_Forwards()
        {
            var (connector, transport) = Create(new SaveResponse());

            var response = await ((IFormApiConnector)connector).SaveAsync(NewDataSet(), CancellationToken.None);

            Assert.IsType<SaveResponse>(response);
            Assert.Equal($"{ProgId}.{FormActions.Save}", transport.LastCall!.Method);
        }

        [Fact]
        [DisplayName("DeleteAsync sends Delete with the row id and returns the affected row count")]
        public async Task DeleteAsync_SendsRowId_ReturnsRowsAffected()
        {
            var rowId = Guid.NewGuid();
            var (connector, transport) = Create(new DeleteResponse { RowsAffected = 1 });

            var response = await connector.DeleteAsync(rowId);

            Assert.Equal($"{ProgId}.{FormActions.Delete}", transport.LastCall!.Method);
            Assert.Equal(rowId, transport.LastCall!.SentValue<DeleteRequest>().RowId);
            Assert.Equal(1, response.RowsAffected);
        }

        [Fact]
        [DisplayName("DeleteAsync through IFormApiConnector forwards to the same Delete call")]
        public async Task DeleteAsync_ThroughContract_Forwards()
        {
            var (connector, transport) = Create(new DeleteResponse { RowsAffected = 1 });

            var response = await ((IFormApiConnector)connector).DeleteAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Equal(1, response.RowsAffected);
            Assert.Equal($"{ProgId}.{FormActions.Delete}", transport.LastCall!.Method);
        }

        [Fact]
        [DisplayName("FieldValueChangeAsync sends FieldValueChange with the table, field, value and row")]
        public async Task FieldValueChangeAsync_SendsRequestFields()
        {
            var (connector, transport) = Create(new FieldValueChangeResponse());

            await connector.FieldValueChangeAsync("Employee", "sys_id", "E002", NewRow());

            Assert.Equal($"{ProgId}.{FormActions.FieldValueChange}", transport.LastCall!.Method);
            var sent = transport.LastCall!.SentValue<FieldValueChangeRequest>();
            Assert.Equal("Employee", sent.TableName);
            Assert.Equal("sys_id", sent.FieldName);
            Assert.Equal("E002", sent.Value);
            Assert.Equal("E001", sent.Row!.Rows[0]["sys_id"]);
        }

        [Fact]
        [DisplayName("FieldValueChangeAsync through IFieldValueChangeConnector forwards to the same call")]
        public async Task FieldValueChangeAsync_ThroughContract_Forwards()
        {
            var (connector, transport) = Create(new FieldValueChangeResponse());

            var response = await ((IFieldValueChangeConnector)connector).FieldValueChangeAsync(
                "Employee", "sys_id", string.Empty, null, CancellationToken.None);

            Assert.IsType<FieldValueChangeResponse>(response);
            Assert.Equal($"{ProgId}.{FormActions.FieldValueChange}", transport.LastCall!.Method);
        }
    }
}
