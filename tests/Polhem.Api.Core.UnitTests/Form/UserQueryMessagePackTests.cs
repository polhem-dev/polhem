using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// Wire-level round trips of the user-query messages through <see cref="MessagePackCodec"/>.
    /// </summary>
    public class UserQueryMessagePackTests
    {
        [Fact]
        [DisplayName("GetUserQueriesResponse round-trips its table with the column types and its default")]
        public void GetUserQueriesResponse_RoundTrip_PreservesMembers()
        {
            var rowId = Guid.NewGuid();
            var table = new DataTable("UserQuery");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_name", typeof(string));
            table.Columns.Add("is_shared", typeof(bool));
            table.Columns.Add("is_own", typeof(bool));
            table.Columns.Add("criteria", typeof(string));
            table.Rows.Add(rowId, "Open orders", true, false, "<UserQuery version=\"1\" />");
            var response = new GetUserQueriesResponse { Table = table, DefaultRowId = rowId };

            var restored = MessagePackCodec.Deserialize<GetUserQueriesResponse>(MessagePackCodec.Serialize(response));

            Assert.NotNull(restored);
            Assert.Equal(rowId, restored!.DefaultRowId);
            var row = Assert.Single(restored.Table!.Rows.Cast<DataRow>());
            Assert.Equal(typeof(bool), restored.Table.Columns["is_own"]!.DataType);
            Assert.Equal((rowId, "Open orders", true, false, "<UserQuery version=\"1\" />"),
                ((Guid)row["sys_rowid"], (string)row["sys_name"], (bool)row["is_shared"], (bool)row["is_own"], (string)row["criteria"]));
        }

        [Fact]
        [DisplayName("GetUserQueriesResponse at its default values round-trips with no table and no default")]
        public void GetUserQueriesResponse_DefaultValues_RoundTrip()
        {
            var restored = MessagePackCodec.Deserialize<GetUserQueriesResponse>(
                MessagePackCodec.Serialize(new GetUserQueriesResponse()));

            Assert.NotNull(restored);
            Assert.Null(restored!.Table);
            Assert.Equal(Guid.Empty, restored.DefaultRowId);
        }

        [Fact]
        [DisplayName("SaveUserQueryRequest with every member populated round-trips intact")]
        public void SaveUserQueryRequest_RoundTrip_PreservesMembers()
        {
            var request = new SaveUserQueryRequest
            {
                RowId = Guid.NewGuid(),
                Name = "本月草稿",
                IsShared = true,
                Criteria = "<UserQuery version=\"1\"><Condition field=\"order_date\" operator=\"Range\" period=\"ThisMonth\" /></UserQuery>",
            };

            var restored = MessagePackCodec.Deserialize<SaveUserQueryRequest>(MessagePackCodec.Serialize(request));

            Assert.NotNull(restored);
            Assert.Equal((request.RowId, request.Name, request.IsShared, request.Criteria),
                (restored!.RowId, restored.Name, restored.IsShared, restored.Criteria));
        }

        [Fact]
        [DisplayName("The row-id messages of saving, deleting and setting a default round-trip their members")]
        public void RowIdMessages_RoundTrip_PreserveMembers()
        {
            var rowId = Guid.NewGuid();

            var saved = MessagePackCodec.Deserialize<SaveUserQueryResponse>(MessagePackCodec.Serialize(new SaveUserQueryResponse { RowId = rowId }));
            var delete = MessagePackCodec.Deserialize<DeleteUserQueryRequest>(MessagePackCodec.Serialize(new DeleteUserQueryRequest { RowId = rowId }));
            var deleted = MessagePackCodec.Deserialize<DeleteUserQueryResponse>(MessagePackCodec.Serialize(new DeleteUserQueryResponse { RowsAffected = 1 }));
            var setDefault = MessagePackCodec.Deserialize<SetDefaultUserQueryRequest>(MessagePackCodec.Serialize(new SetDefaultUserQueryRequest { RowId = rowId }));
            var defaulted = MessagePackCodec.Deserialize<SetDefaultUserQueryResponse>(MessagePackCodec.Serialize(new SetDefaultUserQueryResponse { DefaultRowId = rowId }));

            Assert.Equal(rowId, saved!.RowId);
            Assert.Equal(rowId, delete!.RowId);
            Assert.Equal(1, deleted!.RowsAffected);
            Assert.Equal(rowId, setDefault!.RowId);
            Assert.Equal(rowId, defaulted!.DefaultRowId);
        }
    }
}
