using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.Messages.Form;
using Polhem.Base.Data;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Verifies that when <see cref="Polhem.Api.Client.Connectors.ApiConnector"/> sends a save request, the server
    /// gets a copy of the <c>DataSet</c>: when the server rewrites it in place, the caller's copy is not changed
    /// (ADR-032 D4).
    /// </summary>
    /// <remarks>
    /// An in-process call (<c>LocalApiProvider</c> + <c>Plain</c>) has no serialization boundary, so the server
    /// receives the very object the Connector handed over. <c>FormBusinessObject.Save</c> rewrites the time
    /// columns, and after writing, the adapter also calls <c>AcceptChanges</c>. Without the copy, the document on
    /// screen would turn into UTC values and lose its unsaved state. Here a fake provider rewrites the received
    /// object in place on the "server", reproducing the same shape.
    /// </remarks>
    public class ApiConnectorRequestIsolationTests
    {
        private static readonly DateTime s_callerValue = new(2026, 1, 1, 17, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime s_serverValue = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        [Theory]
        [InlineData("")]
        [InlineData("Asia/Taipei")]
        [DisplayName("When the server rewrites the saved DataSet in place, the caller's DataSet keeps its values and unsaved state, and the sent value is not converted")]
        public async Task ExecuteAsync_ServerRewritesSavedDataSet_LeavesCallerDataSetUntouched(string userTimeZoneId)
        {
            var original = BuildEditedDataSet();
            var request = new SaveRequest { DataSet = original };
            DataSet? received = null;
            DateTime? receivedValue = null;

            await ApiConnectorTestHost.ExecuteAsUserAsync(request, userTimeZoneId, serverRequest =>
            {
                var save = Assert.IsType<SaveRequest>(serverRequest.Params!.Value);
                received = save.DataSet;
                var row = save.DataSet!.Tables[0].Rows[0];
                receivedValue = (DateTime)row["created_at"];

                row["created_at"] = s_serverValue;
                save.DataSet.AcceptChanges();
            });

            Assert.NotNull(received);
            Assert.NotSame(original, received);
            Assert.Equal(s_callerValue, receivedValue);

            Assert.Same(original, request.DataSet);
            var callerRow = original.Tables[0].Rows[0];
            Assert.Equal(DataRowState.Modified, callerRow.RowState);
            Assert.Equal(s_callerValue, (DateTime)callerRow["created_at"]);
        }

        private static DataSet BuildEditedDataSet()
        {
            var table = new DataTable("orders");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.AddColumn("remark", FieldDbType.String);
            table.Rows.Add(s_callerValue, "a");
            table.AcceptChanges();
            table.Rows[0]["remark"] = "edited";

            var dataSet = new DataSet("s");
            dataSet.Tables.Add(table);
            return dataSet;
        }
    }
}
