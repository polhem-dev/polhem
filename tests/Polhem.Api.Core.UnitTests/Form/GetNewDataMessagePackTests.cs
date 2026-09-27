using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// MessagePack round-trip tests of <see cref="GetNewDataRequest"/> / <see cref="GetNewDataResponse"/>. The focus:
    /// a DataSet skeleton (an empty detail table, one Added master row) keeps its structure and RowState after the
    /// wire round-trip.
    /// </summary>
    public class GetNewDataMessagePackTests
    {
        [Fact]
        [DisplayName("GetNewDataRequest has no wire members but still goes through serialization and yields a new instance")]
        public void GetNewDataRequest_Empty_RoundTrips()
        {
            var request = new GetNewDataRequest();

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<GetNewDataRequest>(bytes);

            // This message type declares no wire members of its own, and the inherited `Parameters` is left empty here,
            // so there are no values to compare. What can be asserted is that the formatter is registered and actually ran: it produces
            // non-empty bytes, and restores a **new** instance rather than the same reference. `Assert.NotNull` alone
            // proves neither.
            Assert.NotEmpty(bytes);
            Assert.IsType<GetNewDataRequest>(restored);
            Assert.NotSame(request, restored);
        }

        [Fact]
        [DisplayName("GetNewDataResponse with an Added master row and an empty detail table restores RowState and schema completely")]
        public void GetNewDataResponse_SkeletonDataSet_RoundTripPreservesAddedRowState()
        {
            var dataSet = new DataSet("Employee");

            var master = new DataTable("Employee");
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add(SysFields.Name, typeof(string));
            var masterRowId = Guid.NewGuid();
            master.Rows.Add(masterRowId, "預設員工");
            dataSet.Tables.Add(master);

            var detail = new DataTable("EmployeeDept");
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            dataSet.Tables.Add(detail);

            var response = new GetNewDataResponse { DataSet = dataSet };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetNewDataResponse>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.DataSet);
            Assert.Equal(2, restored.DataSet!.Tables.Count);

            var restoredMaster = restored.DataSet.Tables["Employee"]!;
            Assert.Single(restoredMaster.Rows);
            Assert.Equal(masterRowId, (Guid)restoredMaster.Rows[0][SysFields.RowId]);
            Assert.Equal("預設員工", restoredMaster.Rows[0][SysFields.Name]);
            Assert.Equal(DataRowState.Added, restoredMaster.Rows[0].RowState);

            var restoredDetail = restored.DataSet.Tables["EmployeeDept"]!;
            Assert.Empty(restoredDetail.Rows);
            Assert.Equal(2, restoredDetail.Columns.Count);
        }

        [Fact]
        [DisplayName("GetNewDataResponse.DataSet = null round-trips as null")]
        public void GetNewDataResponse_NullDataSet_RoundTrip()
        {
            var response = new GetNewDataResponse { DataSet = null };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetNewDataResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Null(restored!.DataSet);
        }
    }
}
