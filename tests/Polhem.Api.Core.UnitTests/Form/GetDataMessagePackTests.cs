using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// MessagePack round-trip tests for <see cref="GetDataRequest"/> / <see cref="GetDataResponse"/>: the Master and
    /// Detail tables in the DataSet are restored (with row state Unchanged).
    /// </summary>
    public class GetDataMessagePackTests
    {
        [Fact]
        [DisplayName("GetDataRequest with a RowId round-trips")]
        public void GetDataRequest_RoundTrip_PreservesRowId()
        {
            var rowId = Guid.NewGuid();
            var request = new GetDataRequest { RowId = rowId };

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<GetDataRequest>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(rowId, restored!.RowId);
        }

        [Fact]
        [DisplayName("GetDataRequest with default values round-trips to RowId = Guid.Empty")]
        public void GetDataRequest_DefaultValues_RoundTrip()
        {
            var request = new GetDataRequest();

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<GetDataRequest>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(Guid.Empty, restored!.RowId);
        }

        [Fact]
        [DisplayName("GetDataResponse with Master and Detail tables and Unchanged row states is fully restored")]
        public void GetDataResponse_RoundTrip_PreservesUnchangedRowState()
        {
            var dataSet = new DataSet("Employee");

            var master = new DataTable("Employee");
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add(SysFields.Name, typeof(string));
            var masterRowId = Guid.NewGuid();
            master.Rows.Add(masterRowId, "員工甲");
            dataSet.Tables.Add(master);

            var detail = new DataTable("EmployeeDept");
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Rows.Add(Guid.NewGuid(), masterRowId);
            detail.Rows.Add(Guid.NewGuid(), masterRowId);
            dataSet.Tables.Add(detail);

            dataSet.AcceptChanges();

            var response = new GetDataResponse { DataSet = dataSet };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetDataResponse>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.DataSet);
            Assert.Equal(2, restored.DataSet!.Tables.Count);

            var restoredMaster = restored.DataSet.Tables["Employee"]!;
            Assert.Single(restoredMaster.Rows);
            Assert.Equal(DataRowState.Unchanged, restoredMaster.Rows[0].RowState);

            var restoredDetail = restored.DataSet.Tables["EmployeeDept"]!;
            Assert.Equal(2, restoredDetail.Rows.Count);
            Assert.All(restoredDetail.Rows.Cast<DataRow>(),
                       r => Assert.Equal(DataRowState.Unchanged, r.RowState));
        }

        [Fact]
        [DisplayName("GetDataResponse.DataSet = null round-trips as null")]
        public void GetDataResponse_NullDataSet_RoundTrip()
        {
            var response = new GetDataResponse { DataSet = null };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetDataResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Null(restored!.DataSet);
        }
    }
}
