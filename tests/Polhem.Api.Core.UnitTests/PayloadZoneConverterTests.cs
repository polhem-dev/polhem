using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// <see cref="PayloadZoneConverter"/> tests: the response direction converts into the user's time zone; the request
    /// direction converts only filter conditions, copies the <c>DataSet</c> without converting it, and leaves the
    /// caller's own objects untouched after the call.
    /// </summary>
    public class PayloadZoneConverterTests
    {
        private const string Taipei = "Asia/Taipei";
        private static readonly DateTime s_utc9Am = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        private static DateTime ExpectedInTaipei(DateTime utcValue)
            => DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(utcValue, DateTimeKind.Unspecified),
                    TimeZoneInfo.FindSystemTimeZoneById(Taipei)),
                DateTimeKind.Unspecified);

        private static DataTable BuildTable()
        {
            var table = new DataTable("orders");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.Rows.Add(s_utc9Am);
            table.AcceptChanges();
            return table;
        }

        private static DataSet BuildDataSet()
        {
            var dataSet = new DataSet("s");
            dataSet.Tables.Add(BuildTable());
            return dataSet;
        }

        [Fact]
        [DisplayName("Response direction: GetListResponse.Table is converted to the user's time zone")]
        public void ToUserZone_GetListResponse_ConvertsTable()
        {
            var response = new GetListResponse { Table = BuildTable() };

            PayloadZoneConverter.ToUserZone(response, Taipei);

            Assert.Equal(ExpectedInTaipei(s_utc9Am), (DateTime)response.Table!.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("Response direction: GetDataResponse.DataSet is converted to the user's time zone")]
        public void ToUserZone_GetDataResponse_ConvertsDataSet()
        {
            var response = new GetDataResponse { DataSet = BuildDataSet() };

            PayloadZoneConverter.ToUserZone(response, Taipei);

            Assert.Equal(ExpectedInTaipei(s_utc9Am),
                (DateTime)response.DataSet!.Tables["orders"]!.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("Request direction: while sending, the DataSet is an unconverted copy, and the caller's object is unchanged after the server rewrites the copy")]
        public void IsolateRequest_SaveRequest_CopiesWithoutConvertingAndRestores()
        {
            var original = BuildDataSet();
            // The caller holds values in the user's time zone (the Connector already converted them when the response arrived).
            var userLocal = ExpectedInTaipei(s_utc9Am);
            original.Tables["orders"]!.Rows[0]["created_at"] = userLocal;
            original.AcceptChanges();
            var request = new SaveRequest { DataSet = original };

            using (PayloadZoneConverter.IsolateRequest(request, Taipei))
            {
                Assert.NotSame(original, request.DataSet);
                var sent = request.DataSet!.Tables["orders"]!.Rows[0];
                Assert.Equal(userLocal, (DateTime)sent["created_at"]);

                // In process, the server receives this very object: it rewrites the time column and calls `AcceptChanges` after writing.
                sent["created_at"] = s_utc9Am;
                request.DataSet.AcceptChanges();
            }

            Assert.Same(original, request.DataSet);
            Assert.Equal(userLocal, (DateTime)original.Tables["orders"]!.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("Request direction: without a user time zone the DataSet is still copied and the filter is sent as is")]
        public void IsolateRequest_BlankTimeZone_CopiesDataSetAndLeavesFilter()
        {
            var original = BuildDataSet();
            var saveRequest = new SaveRequest { DataSet = original };
            using (PayloadZoneConverter.IsolateRequest(saveRequest, string.Empty))
            {
                Assert.NotSame(original, saveRequest.DataSet);
            }
            Assert.Same(original, saveRequest.DataSet);

            var filter = FilterCondition.Equal("created_at", s_utc9Am);
            var listRequest = new GetListRequest { Filter = filter };
            using (PayloadZoneConverter.IsolateRequest(listRequest, string.Empty))
            {
                Assert.Same(filter, listRequest.Filter);
            }
        }

        [Fact]
        [DisplayName("Request direction: DateTime values in the filter are converted to UTC, DateOnly is untouched, and the original tree is not modified")]
        public void IsolateRequest_GetListRequest_ConvertsFilterWithoutMutatingSource()
        {
            var userLocal = ExpectedInTaipei(s_utc9Am);
            var day = new DateOnly(2026, 1, 1);
            var filter = FilterGroup.All(
                FilterCondition.Equal("created_at", userLocal),
                FilterCondition.Equal("order_date", day));
            var request = new GetListRequest { Filter = filter };

            using (PayloadZoneConverter.IsolateRequest(request, Taipei))
            {
                var converted = (FilterGroup)request.Filter!;
                Assert.Equal(s_utc9Am, ((FilterCondition)converted.Nodes[0]).Value);
                Assert.Equal(day, ((FilterCondition)converted.Nodes[1]).Value);
            }

            Assert.Same(filter, request.Filter);
            Assert.Equal(userLocal, ((FilterCondition)filter.Nodes[0]).Value);
        }

        [Fact]
        [DisplayName("Response direction: a blank time zone is a no-op")]
        public void ToUserZone_BlankTimeZone_LeavesPayloadAlone()
        {
            var response = new GetListResponse { Table = BuildTable() };

            PayloadZoneConverter.ToUserZone(response, string.Empty);

            Assert.Equal(s_utc9Am, (DateTime)response.Table!.Rows[0]["created_at"]);
        }

        [Fact]
        [DisplayName("Unsupported types and null are ignored")]
        public void UnknownPayload_IsIgnored()
        {
            Assert.Null(Record.Exception(() => PayloadZoneConverter.ToUserZone("plain", Taipei)));
            Assert.Null(Record.Exception(() => PayloadZoneConverter.ToUserZone(null, Taipei)));
            using (PayloadZoneConverter.IsolateRequest(null, Taipei)) { }
            using (PayloadZoneConverter.IsolateRequest("plain", Taipei)) { }
        }
    }
}
