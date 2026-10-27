using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// Wire-level round trips of <see cref="FieldValueChangeRequest"/> and <see cref="FieldValueChangeResponse"/> through
    /// <see cref="MessagePackCodec"/>: the edited row and the values travel as typed tables, and the message maps as
    /// string dictionaries.
    /// </summary>
    public class FieldValueChangeMessagePackTests
    {
        [Fact]
        [DisplayName("FieldValueChangeRequest with every member populated round-trips intact, the row keeping its column types")]
        public void FieldValueChangeRequest_RoundTrip_PreservesMembers()
        {
            var rowId = Guid.NewGuid();
            var row = new DataTable("OrderDetail");
            row.Columns.Add("part_rowid", typeof(Guid));
            row.Columns.Add("qty", typeof(decimal));
            row.Rows.Add(rowId, 12.5m);
            var request = new FieldValueChangeRequest
            {
                TableName = "OrderDetail",
                FieldName = "part_rowid",
                Value = rowId.ToString(),
                Row = row,
            };

            var restored = MessagePackCodec.Deserialize<FieldValueChangeRequest>(MessagePackCodec.Serialize(request));

            Assert.NotNull(restored);
            Assert.Equal("OrderDetail", restored!.TableName);
            Assert.Equal("part_rowid", restored.FieldName);
            Assert.Equal(rowId.ToString(), restored.Value);
            Assert.NotNull(restored.Row);
            Assert.Equal(typeof(Guid), restored.Row!.Columns["part_rowid"]!.DataType);
            Assert.Equal(rowId, restored.Row.Rows[0]["part_rowid"]);
            Assert.Equal(12.5m, restored.Row.Rows[0]["qty"]);
        }

        [Fact]
        [DisplayName("FieldValueChangeRequest at its default values round-trips with an empty value and no row")]
        public void FieldValueChangeRequest_DefaultValues_RoundTrip()
        {
            var restored = MessagePackCodec.Deserialize<FieldValueChangeRequest>(
                MessagePackCodec.Serialize(new FieldValueChangeRequest()));

            Assert.NotNull(restored);
            Assert.Equal(string.Empty, restored!.Value);
            Assert.Null(restored.Row);
        }

        [Fact]
        [DisplayName("FieldValueChangeResponse round-trips its values, including a cleared one, and its errors and warnings")]
        public void FieldValueChangeResponse_RoundTrip_PreservesMembers()
        {
            var values = new DataTable("Values");
            values.Columns.Add("ref_part_name", typeof(string));
            values.Columns.Add("unit_price", typeof(decimal));
            values.Columns.Add("warehouse_rowid", typeof(Guid));
            values.Rows.Add("Bolt", 3.25m, DBNull.Value);
            var response = new FieldValueChangeResponse { Values = values };
            response.Errors["qty"] = "Not enough stock.";
            response.Warnings["part_rowid"] = "Below safety stock.";

            var restored = MessagePackCodec.Deserialize<FieldValueChangeResponse>(MessagePackCodec.Serialize(response));

            Assert.NotNull(restored);
            Assert.Equal("Bolt", restored!.Values!.Rows[0]["ref_part_name"]);
            Assert.Equal(3.25m, restored.Values.Rows[0]["unit_price"]);
            Assert.Equal(DBNull.Value, restored.Values.Rows[0]["warehouse_rowid"]);
            Assert.Equal("Not enough stock.", restored.Errors["qty"]);
            Assert.Equal("Below safety stock.", restored.Warnings["part_rowid"]);
        }

        [Fact]
        [DisplayName("FieldValueChangeResponse at its default values round-trips with no values and empty message maps")]
        public void FieldValueChangeResponse_DefaultValues_RoundTrip()
        {
            var restored = MessagePackCodec.Deserialize<FieldValueChangeResponse>(
                MessagePackCodec.Serialize(new FieldValueChangeResponse()));

            Assert.NotNull(restored);
            Assert.Null(restored!.Values);
            Assert.Empty(restored.Errors);
            Assert.Empty(restored.Warnings);
        }
    }
}
