using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Null round-trip tests for DataSetFormatter / DataTableFormatter, covering the WriteNil/TryReadNil paths.
    /// </summary>
    public class MessagePackNullFormatterTests
    {
        [Fact]
        [DisplayName("A null DataSet serializes and deserializes back to null")]
        public void DataSet_Null_Serialize_RoundTrip_ReturnsNull()
        {
            DataSet? original = null;

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<DataSet?>(bytes);

            Assert.Null(restored);
        }

        [Fact]
        [DisplayName("A null DataTable serializes and deserializes back to null")]
        public void DataTable_Null_Serialize_RoundTrip_ReturnsNull()
        {
            DataTable? original = null;

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<DataTable?>(bytes);

            Assert.Null(restored);
        }

        [Fact]
        [DisplayName("An empty DataSet round-trips to an empty table collection")]
        public void DataSet_Empty_Serialize_RoundTrip_ReturnsEmpty()
        {
            var original = new DataSet("Empty");

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<DataSet>(bytes);

            Assert.NotNull(restored);
            Assert.Empty(restored.Tables);
        }

        [Fact]
        [DisplayName("An empty DataTable round-trips to an empty table")]
        public void DataTable_Empty_Serialize_RoundTrip_ReturnsEmpty()
        {
            var original = new DataTable("Empty");
            original.Columns.Add("Id", typeof(int));

            var bytes = MessagePackCodec.Serialize(original);
            var restored = MessagePackCodec.Deserialize<DataTable>(bytes);

            Assert.NotNull(restored);
            Assert.Empty(restored.Rows);
            Assert.Single(restored.Columns.Cast<DataColumn>());
        }
    }
}
