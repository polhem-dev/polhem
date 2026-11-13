using System.Buffers;
using System.ComponentModel;
using System.Data;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Core.Data;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// How the MessagePack DataTable reader treats entries shorter or longer than the layout it knows: a short table,
    /// column or row entry is refused, while trailing fields a newer writer appends are skipped.
    /// </summary>
    public class DataTableFormatterMalformedTests
    {
        private delegate void WriteBody(ref MessagePackWriter writer);

        private static byte[] Build(WriteBody write)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            write(ref writer);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        private static void WriteColumn(ref MessagePackWriter writer, string name, int fieldCount = 7)
        {
            writer.WriteArrayHeader(fieldCount);
            writer.Write(name);
            writer.Write((int)FieldDbType.Integer);
            writer.Write(string.Empty);
            writer.Write(true);
            writer.Write(false);
            writer.Write(-1);
            writer.WriteNil();
            for (var i = 7; i < fieldCount; i++)
                writer.Write("future");
        }

        [Fact]
        [DisplayName("A table entry with fewer fields than the layout is refused")]
        public void Deserialize_ShortTableEntry_Throws()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(3);
                w.Write("T");
                w.WriteArrayHeader(0);
                w.WriteArrayHeader(0);
            });

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));
        }

        [Fact]
        [DisplayName("Trailing fields after the table's rows are skipped and the table still reads")]
        public void Deserialize_TableEntryWithTrailingFields_SkipsThem()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(6);
                w.Write("T");
                w.WriteArrayHeader(1);
                WriteColumn(ref w, "Id");
                w.WriteArrayHeader(0);
                w.WriteArrayHeader(0);
                w.Write("future");
                w.Write(1);
            });

            var table = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            Assert.Equal("T", table.TableName);
            Assert.Equal("Id", Assert.Single(table.Columns.Cast<DataColumn>()).ColumnName);
        }

        [Fact]
        [DisplayName("A column entry with fewer fields than the layout is refused")]
        public void Deserialize_ShortColumnEntry_Throws()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(4);
                w.Write("T");
                w.WriteArrayHeader(1);
                w.WriteArrayHeader(6);
                w.Write("Id");
                w.Write((int)FieldDbType.Integer);
                w.Write(string.Empty);
                w.Write(true);
                w.Write(false);
                w.Write(-1);
                w.WriteArrayHeader(0);
                w.WriteArrayHeader(0);
            });

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));
        }

        [Fact]
        [DisplayName("Trailing fields of a column entry are skipped and the column still reads")]
        public void Deserialize_ColumnEntryWithTrailingFields_SkipsThem()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(4);
                w.Write("T");
                w.WriteArrayHeader(1);
                WriteColumn(ref w, "Id", fieldCount: 9);
                w.WriteArrayHeader(0);
                w.WriteArrayHeader(0);
            });

            var table = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            Assert.Equal(typeof(int), table.Columns["Id"]!.DataType);
        }

        [Fact]
        [DisplayName("A nil name in the primary key list is dropped")]
        public void Deserialize_NilPrimaryKeyName_IsDropped()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(4);
                w.Write("T");
                w.WriteArrayHeader(1);
                WriteColumn(ref w, "Id");
                w.WriteArrayHeader(2);
                w.WriteNil();
                w.Write("Id");
                w.WriteArrayHeader(0);
            });

            var table = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            Assert.Equal("Id", Assert.Single(table.PrimaryKey).ColumnName);
        }

        [Fact]
        [DisplayName("A row entry with no row state is refused")]
        public void Deserialize_EmptyRowEntry_Throws()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(4);
                w.Write("T");
                w.WriteArrayHeader(1);
                WriteColumn(ref w, "Id");
                w.WriteArrayHeader(0);
                w.WriteArrayHeader(1);
                w.WriteArrayHeader(0);
            });

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));
        }

        [Fact]
        [DisplayName("A row entry with an unknown row state is refused")]
        public void Deserialize_UnknownRowState_Throws()
        {
            var bytes = DataTableWire.WriteSingleRowTable((int)DataRowState.Detached, 1);

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));
        }

        [Fact]
        [DisplayName("A Modified row entry with only one set of values is refused")]
        public void Deserialize_ModifiedRowWithOneValueSet_Throws()
        {
            var bytes = DataTableWire.WriteSingleRowTable((int)DataRowState.Modified, 1);

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));
        }

        [Fact]
        [DisplayName("A column that declares an undefined field type is refused")]
        public void Deserialize_UndefinedFieldType_Throws()
        {
            var bytes = DataTableWire.WriteColumnWithFieldType(9999);

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));
        }
    }
}
