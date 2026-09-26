using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Polhem.Api.Core.MessagePack;
using Polhem.Base.Data;
using Polhem.Base.Serialization;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Verifies that column semantic markers are carried over the MessagePack wire path and agree with the JSON path.
    /// Wire serialization has parallel MessagePack and JSON implementations (in Polhem.Api.Core and Polhem.Base),
    /// so changing one and forgetting the other is the most likely mistake. A deployment usually runs only one
    /// PayloadFormat and breaks only when it switches, so consistency between the formats must be pinned by tests.
    /// </summary>
    public class FieldDbTypeWireMarkerTests
    {
        private static JsonSerializerOptions JsonOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new DataTableJsonConverter());
            return opts;
        }

        private static DataTable BuildTable()
        {
            var table = new DataTable("orders");
            table.AddColumn("order_date", FieldDbType.Date, DateTime.Today);
            table.AddColumn("created_at", FieldDbType.DateTime, DateTime.Now);
            table.AddColumn("remark", FieldDbType.Text, string.Empty);
            table.AddColumn("amount", FieldDbType.Currency, 0m);
            return table;
        }

        [Fact]
        [DisplayName("MessagePack round-trip preserves the Date marker instead of falling back to DateTime")]
        public void MessagePackRoundTrip_PreservesDateMarker()
        {
            var table = BuildTable();

            var bytes = MessagePackCodec.Serialize(table);
            var restored = MessagePackCodec.Deserialize<DataTable>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(FieldDbType.Date, restored!.Columns["order_date"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.DateTime, restored.Columns["created_at"]!.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("SerializableDataTable carries Date as the wire column type")]
        public void SerializableDataTable_CarriesDateOnWire()
        {
            var table = BuildTable();

            var sdt = SerializableDataTable.FromDataTable(table);

            var column = Assert.Single(sdt.Columns, c => c.ColumnName == "order_date");
            Assert.Equal(FieldDbType.Date, column.DataType);
        }

        [Fact]
        [DisplayName("A calendar-day column keeps DateTime as its CLR type after a MessagePack round-trip")]
        public void MessagePackRoundTrip_DateColumnStaysDateTimeClrType()
        {
            var table = BuildTable();
            var row = table.NewRow();
            row["order_date"] = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Unspecified);
            table.Rows.Add(row);

            var bytes = MessagePackCodec.Serialize(table);
            var restored = MessagePackCodec.Deserialize<DataTable>(bytes);

            Assert.Equal(typeof(DateTime), restored!.Columns["order_date"]!.DataType);
            Assert.Equal(new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Unspecified), restored.Rows[0]["order_date"]);
        }

        [Fact]
        [DisplayName("Column markers are identical after a round-trip through either wire format")]
        public void BothWireFormats_ProduceIdenticalMarkers()
        {
            var table = BuildTable();

            var viaMessagePack = MessagePackCodec.Deserialize<DataTable>(MessagePackCodec.Serialize(table));
            var viaJson = JsonSerializer.Deserialize<DataTable>(
                JsonSerializer.Serialize(table, JsonOptions()), JsonOptions());

            Assert.NotNull(viaMessagePack);
            Assert.NotNull(viaJson);

            foreach (DataColumn source in table.Columns)
            {
                var expected = source.ResolveFieldDbType();
                Assert.Equal(expected, viaMessagePack!.Columns[source.ColumnName]!.ResolveFieldDbType());
                Assert.Equal(expected, viaJson!.Columns[source.ColumnName]!.ResolveFieldDbType());
            }
        }

        [Fact]
        [DisplayName("An unmarked DataTable behaves the same after a MessagePack round-trip")]
        public void MessagePackRoundTrip_UnmarkedTable_BehaviourUnchanged()
        {
            var table = new DataTable("t");
            table.Columns.Add("created_at", typeof(DateTime));
            table.Columns.Add("name", typeof(string));

            var restored = MessagePackCodec.Deserialize<DataTable>(MessagePackCodec.Serialize(table));

            Assert.Equal(FieldDbType.DateTime, restored!.Columns["created_at"]!.ResolveFieldDbType());
            Assert.Equal(FieldDbType.String, restored.Columns["name"]!.ResolveFieldDbType());
        }

        [Fact]
        [DisplayName("DataSet MessagePack round-trip preserves column markers per table")]
        public void DataSetRoundTrip_PreservesMarkersPerTable()
        {
            var dataSet = new DataSet("ds");
            dataSet.Tables.Add(BuildTable());

            var restored = MessagePackCodec.Deserialize<DataSet>(MessagePackCodec.Serialize(dataSet));

            Assert.NotNull(restored);
            Assert.Equal(FieldDbType.Date, restored!.Tables["orders"]!.Columns["order_date"]!.ResolveFieldDbType());
        }
    }
}
