using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// MessagePack round-trip tests of <see cref="SaveRequest"/> / <see cref="SaveResponse"/>. The focus: restoring
    /// a DataSet that mixes <see cref="DataRowState"/> values (Unchanged / Added / Modified / Deleted), and the
    /// AffectedRows dictionary.
    /// </summary>
    public class SaveMessagePackTests
    {
        [Fact]
        [DisplayName("A SaveRequest DataSet with mixed Unchanged/Added/Modified/Deleted row states is fully restored")]
        public void SaveRequest_DataSet_PreservesMixedRowStates()
        {
            var dataSet = new DataSet("Employee");

            var master = new DataTable("Employee");
            var rowIdColumn = master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add(SysFields.Name, typeof(string));
            master.PrimaryKey = new[] { rowIdColumn };

            var unchangedId = Guid.NewGuid();
            var modifiedId = Guid.NewGuid();
            var deletedId = Guid.NewGuid();
            var addedId = Guid.NewGuid();

            master.Rows.Add(unchangedId, "保持不變");
            master.Rows.Add(modifiedId, "原始名稱");
            master.Rows.Add(deletedId, "待刪除");
            master.AcceptChanges();

            master.Rows.Find(modifiedId)![SysFields.Name] = "已修改名稱";
            master.Rows.Find(deletedId)!.Delete();
            master.Rows.Add(addedId, "全新一筆");

            dataSet.Tables.Add(master);

            var request = new SaveRequest { DataSet = dataSet };

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<SaveRequest>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.DataSet);
            var restoredMaster = restored.DataSet!.Tables["Employee"]!;

            // Every row, including the Deleted one, must survive with its RowState.
            DataRow? FindByCurrentOrOriginal(Guid id)
            {
                foreach (DataRow r in restoredMaster.Rows)
                {
                    var version = r.RowState == DataRowState.Deleted
                        ? DataRowVersion.Original
                        : DataRowVersion.Current;
                    if (r[SysFields.RowId, version] is Guid g && g == id)
                        return r;
                }
                return null;
            }

            Assert.Equal(DataRowState.Unchanged, FindByCurrentOrOriginal(unchangedId)!.RowState);
            Assert.Equal(DataRowState.Modified, FindByCurrentOrOriginal(modifiedId)!.RowState);
            Assert.Equal(DataRowState.Deleted, FindByCurrentOrOriginal(deletedId)!.RowState);
            Assert.Equal(DataRowState.Added, FindByCurrentOrOriginal(addedId)!.RowState);
        }

        [Fact]
        [DisplayName("SaveResponse with AffectedRows and a refreshed DataSet round-trips intact")]
        public void SaveResponse_RoundTrip_PreservesAffectedRowsAndDataSet()
        {
            var dataSet = new DataSet("Employee");
            var master = new DataTable("Employee");
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add(SysFields.Name, typeof(string));
            master.Rows.Add(Guid.NewGuid(), "回寫後資料");
            dataSet.Tables.Add(master);
            dataSet.AcceptChanges();

            var response = new SaveResponse
            {
                DataSet = dataSet,
                AffectedRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Employee"] = 2,
                    ["EmployeeDept"] = 3,
                },
            };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<SaveResponse>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.DataSet);
            Assert.Single(restored.DataSet!.Tables["Employee"]!.Rows);
            Assert.Equal(2, restored.AffectedRows["Employee"]);
            Assert.Equal(3, restored.AffectedRows["EmployeeDept"]);
        }

        [Fact]
        [DisplayName("SaveResponse with an empty AffectedRows dictionary round-trips as an empty dictionary")]
        public void SaveResponse_EmptyAffectedRows_RoundTrip()
        {
            var response = new SaveResponse();

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<SaveResponse>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.AffectedRows);
            Assert.Empty(restored.AffectedRows);
            Assert.Null(restored.DataSet);
        }
    }
}
