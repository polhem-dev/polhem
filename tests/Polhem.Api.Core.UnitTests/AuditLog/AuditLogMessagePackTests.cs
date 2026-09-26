using System.ComponentModel;
using System.Data;
using Polhem.Api.Contracts.AuditLog;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Definition.Logging;
using Polhem.Definition.Paging;

namespace Polhem.Api.Core.UnitTests.AuditLog
{
    /// <summary>
    /// Round-trip verification of the AuditLog wire DTOs through <see cref="MessagePackCodec"/>: the <c>DataTable</c> and
    /// <c>PagingInfo</c> of list responses, the nested <c>List&lt;RecordFieldChange&gt;</c> of the detail response, and
    /// the typed filter fields of <c>GetChangeLogRequest</c> (including a nullable enum) are all restored in full.
    /// </summary>
    public class AuditLogMessagePackTests
    {
        [Fact]
        [DisplayName("GetChangeLogRequest round-trips its typed filter, including a nullable enum and paging")]
        public void GetChangeLogRequest_RoundTrip()
        {
            var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var request = new GetChangeLogRequest
            {
                FromUtc = from,
                UserId = "demo",
                ProgId = "Employee",
                ChangeKind = ChangeKind.Delete,
                Paging = new PagingOptions { Page = 2, PageSize = 25, IncludeTotalCount = true },
            };

            var restored = MessagePackCodec.Deserialize<GetChangeLogRequest>(MessagePackCodec.Serialize(request));

            Assert.NotNull(restored);
            Assert.Equal(from, restored!.FromUtc);
            Assert.Null(restored.ToUtc);
            Assert.Equal("demo", restored.UserId);
            Assert.Equal("Employee", restored.ProgId);
            Assert.Equal(ChangeKind.Delete, restored.ChangeKind);
            Assert.Equal(2, restored.Paging!.Page);
            Assert.True(restored.Paging.IncludeTotalCount);
        }

        [Fact]
        [DisplayName("GetChangeDetailResponse with nested Fields round-trips in full")]
        public void GetChangeDetailResponse_RoundTrip_PreservesFields()
        {
            var sysRowId = Guid.NewGuid();
            var response = new GetChangeDetailResponse
            {
                SysRowId = sysRowId,
                LogTime = new DateTime(2026, 7, 8, 3, 0, 0, DateTimeKind.Utc),
                UserId = "demo",
                ProgId = "Employee",
                RowKey = "R-1",
                ChangeKind = ChangeKind.Update,
                IsSensitive = true,
                Source = "Employee.Save",
                Fields =
                [
                    new RecordFieldChange { TableName = "st_employee", RowKey = "R-1", RowState = ChangeKind.Update, FieldName = "name", OldValue = "Alice", NewValue = "Alice Wang" },
                    new RecordFieldChange { TableName = "st_employee", RowKey = "R-1", RowState = ChangeKind.Update, FieldName = "note", OldValue = "keep", NewValue = null },
                ],
            };

            var restored = MessagePackCodec.Deserialize<GetChangeDetailResponse>(MessagePackCodec.Serialize(response));

            Assert.NotNull(restored);
            Assert.Equal(sysRowId, restored!.SysRowId);
            Assert.Equal(ChangeKind.Update, restored.ChangeKind);
            Assert.True(restored.IsSensitive);
            Assert.Equal(2, restored.Fields.Count);
            Assert.Equal("name", restored.Fields[0].FieldName);
            Assert.Equal("Alice Wang", restored.Fields[0].NewValue);
            Assert.Null(restored.Fields[1].NewValue);
        }

        [Fact]
        [DisplayName("GetLoginLogRequest round-trips a nullable LoginEvent and paging")]
        public void GetLoginLogRequest_RoundTrip()
        {
            var request = new GetLoginLogRequest
            {
                UserId = "demo",
                Event = LoginEvent.LockedOut,
                Paging = new PagingOptions { Page = 3, PageSize = 20 },
            };

            var restored = MessagePackCodec.Deserialize<GetLoginLogRequest>(MessagePackCodec.Serialize(request));

            Assert.NotNull(restored);
            Assert.Equal("demo", restored!.UserId);
            Assert.Equal(LoginEvent.LockedOut, restored.Event);
            Assert.Null(restored.FromUtc);
            Assert.Equal(3, restored.Paging!.Page);
        }

        [Fact]
        [DisplayName("LogListResponse with a DataTable and PagingInfo round-trips (shared by the login, access and anomaly logs)")]
        public void LogListResponse_RoundTrip()
        {
            var table = new DataTable("st_log_login");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("event", typeof(int));
            var id = Guid.NewGuid();
            table.Rows.Add(id, (int)LoginEvent.LoginSucceeded);

            var response = new LogListResponse
            {
                Table = table,
                Paging = new PagingInfo { Page = 1, PageSize = 50, TotalCount = 1, HasMore = false },
            };

            var restored = MessagePackCodec.Deserialize<LogListResponse>(MessagePackCodec.Serialize(response));

            Assert.NotNull(restored);
            Assert.Single(restored!.Table!.Rows);
            Assert.Equal(id, (Guid)restored.Table.Rows[0]["sys_rowid"]);
            Assert.Equal(1, restored.Paging!.TotalCount);
        }

        [Fact]
        [DisplayName("GetTopApiMethodsRequest round-trips its time window and TopN")]
        public void GetTopApiMethodsRequest_RoundTrip()
        {
            var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var request = new GetTopApiMethodsRequest { FromUtc = from, TopN = 25 };

            var restored = MessagePackCodec.Deserialize<GetTopApiMethodsRequest>(MessagePackCodec.Serialize(request));

            Assert.NotNull(restored);
            Assert.Equal(from, restored!.FromUtc);
            Assert.Null(restored.ToUtc);
            Assert.Equal(25, restored.TopN);
        }

        [Fact]
        [DisplayName("LogAggregateResponse with an aggregate DataTable round-trips (shared by summary and top-N)")]
        public void LogAggregateResponse_RoundTrip()
        {
            var table = new DataTable("agg");
            table.Columns.Add("anomaly_kind", typeof(int));
            table.Columns.Add("event_count", typeof(long));
            table.Rows.Add((int)ChangeKind.Update, 42L);

            var restored = MessagePackCodec.Deserialize<LogAggregateResponse>(
                MessagePackCodec.Serialize(new LogAggregateResponse { Table = table }));

            Assert.NotNull(restored);
            Assert.Single(restored!.Table!.Rows);
            Assert.Equal(42L, (long)restored.Table.Rows[0]["event_count"]);
        }

        [Fact]
        [DisplayName("GetChangeDetailResponse DataSet round-trip preserves row states and original values")]
        public void GetChangeDetailResponse_RoundTrip_PreservesDataSetRowStates()
        {
            var response = new GetChangeDetailResponse
            {
                SysRowId = Guid.NewGuid(),
                ChangeKind = ChangeKind.Update,
                DataSet = NewChangeDataSet(),
            };

            var restored = MessagePackCodec.Deserialize<GetChangeDetailResponse>(MessagePackCodec.Serialize(response));

            Assert.NotNull(restored?.DataSet);
            AssertChangeDataSet(restored!.DataSet!);
        }

        /// <summary>One Modified row (Alice → Alice Wang) and one Unchanged row.</summary>
        internal static DataSet NewChangeDataSet()
        {
            var dataSet = new DataSet("Employee");
            var table = dataSet.Tables.Add("Employee");
            table.Columns.Add("sys_rowid", typeof(string));
            table.Columns.Add("name", typeof(string));
            var modified = table.Rows.Add("R-1", "Alice");
            table.Rows.Add("R-2", "Bob");
            dataSet.AcceptChanges();
            modified["name"] = "Alice Wang";
            return dataSet;
        }

        internal static void AssertChangeDataSet(DataSet dataSet)
        {
            var rows = dataSet.Tables["Employee"]!.Rows;
            Assert.Equal(2, rows.Count);
            Assert.Equal(DataRowState.Modified, rows[0].RowState);
            Assert.Equal("Alice", rows[0]["name", DataRowVersion.Original]);
            Assert.Equal("Alice Wang", rows[0]["name", DataRowVersion.Current]);
            Assert.Equal(DataRowState.Unchanged, rows[1].RowState);
            Assert.Equal("Bob", rows[1]["name"]);
        }

        [Fact]
        [DisplayName("GetChangeDetailResponse with empty Fields round-trips without a NullReferenceException")]
        public void GetChangeDetailResponse_EmptyFields_RoundTrip()
        {
            var response = new GetChangeDetailResponse { SysRowId = Guid.NewGuid() };
            var restored = MessagePackCodec.Deserialize<GetChangeDetailResponse>(MessagePackCodec.Serialize(response));
            Assert.NotNull(restored);
            Assert.NotNull(restored!.Fields);
            Assert.Empty(restored.Fields);
        }
    }
}
