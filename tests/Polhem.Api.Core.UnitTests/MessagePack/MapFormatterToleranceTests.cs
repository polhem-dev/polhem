using System.Buffers;
using System.ComponentModel;
using System.Data;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Identity;
using Polhem.Definition.Numbers;
using Polhem.Definition.Organization;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// How the hand-written map formatters treat a null value and a key this version does not know, and how the
    /// DataSet formatter treats entries shorter or longer than its layout and relations that name what the DataSet
    /// does not have.
    /// </summary>
    public class MapFormatterToleranceTests
    {
        public static TheoryData<Type> MapTypes =>
        [
            typeof(CompanyInfo),
            typeof(Parameter),
            typeof(NumberFormatItem),
            typeof(CashRoundingItem),
            typeof(AllowedCurrencyItem),
            typeof(DepartmentNode),
        ];

        private delegate void WriteBody(ref MessagePackWriter writer);

        private static byte[] Build(WriteBody write)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            write(ref writer);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        [Theory]
        [MemberData(nameof(MapTypes))]
        [DisplayName("A null value serializes as nil and nil deserializes back to null")]
        public void Serialize_Null_RoundTripsAsNil(Type type)
        {
            var bytes = MessagePackCodec.Serialize(null!, type);

            Assert.Equal([MessagePackCode.Nil], bytes);
            Assert.Null(MessagePackCodec.Deserialize(bytes, type));
        }

        [Theory]
        [MemberData(nameof(MapTypes))]
        [DisplayName("A key the formatter does not know is skipped and the value still reads")]
        public void Deserialize_UnknownKey_IsSkipped(Type type)
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteMapHeader(1);
                w.Write("AddedInAFutureVersion");
                w.WriteArrayHeader(2);
                w.Write(1);
                w.Write("two");
            });

            Assert.IsType(type, MessagePackCodec.Deserialize(bytes, type));
        }

        private static void WriteIdTable(ref MessagePackWriter w, string tableName)
        {
            w.WriteArrayHeader(4);
            w.Write(tableName);
            w.WriteArrayHeader(1);
            w.WriteArrayHeader(7);
            w.Write("Id");
            w.Write((int)FieldDbType.Integer);
            w.Write(string.Empty);
            w.Write(true);
            w.Write(false);
            w.Write(-1);
            w.WriteNil();
            w.WriteArrayHeader(0);
            w.WriteArrayHeader(0);
        }

        /// <summary>A DataSet of tables <c>P</c> and <c>C</c>, each with an <c>Id</c> column, and one relation entry.</summary>
        private static byte[] DataSetWithRelation(WriteBody writeRelation, int dataSetFieldCount = 3)
            => Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(dataSetFieldCount);
                w.Write("DS");
                w.WriteArrayHeader(2);
                WriteIdTable(ref w, "P");
                WriteIdTable(ref w, "C");
                w.WriteArrayHeader(1);
                writeRelation(ref w);
                for (var i = 3; i < dataSetFieldCount; i++)
                    w.Write("future");
            });

        private static void WriteRelation(ref MessagePackWriter w, string? parent, string? child, string? childColumn, int fieldCount = 5)
        {
            w.WriteArrayHeader(fieldCount);
            w.Write("P_C");
            w.Write(parent);
            w.Write(child);
            w.WriteArrayHeader(1);
            w.Write("Id");
            w.WriteArrayHeader(1);
            w.Write(childColumn);
            for (var i = 5; i < fieldCount; i++)
                w.Write("future");
        }

        [Fact]
        [DisplayName("A DataSet entry with fewer fields than the layout is refused")]
        public void DataSet_ShortEntry_Throws()
        {
            var bytes = Build((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(2);
                w.Write("DS");
                w.WriteArrayHeader(0);
            });

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataSet>(bytes));
        }

        [Fact]
        [DisplayName("Trailing fields of a DataSet entry and of a relation entry are skipped and the relation still reads")]
        public void DataSet_TrailingFields_AreSkipped()
        {
            var bytes = DataSetWithRelation((ref MessagePackWriter w) => WriteRelation(ref w, "P", "C", "Id", fieldCount: 6),
                dataSetFieldCount: 4);

            var dataSet = MessagePackCodec.Deserialize<DataSet>(bytes)!;

            var relation = Assert.Single(dataSet.Relations.Cast<DataRelation>());
            Assert.Equal("P", relation.ParentTable.TableName);
            Assert.Equal("C", relation.ChildTable.TableName);
        }

        [Fact]
        [DisplayName("A relation entry with fewer fields than the layout is refused")]
        public void DataSet_ShortRelationEntry_Throws()
        {
            var bytes = DataSetWithRelation((ref MessagePackWriter w) =>
            {
                w.WriteArrayHeader(4);
                w.Write("P_C");
                w.Write("P");
                w.Write("C");
                w.WriteArrayHeader(0);
            });

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataSet>(bytes));
        }

        [Theory]
        [InlineData("Missing", "C")]
        [InlineData(null, "C")]
        [InlineData("P", null)]
        [DisplayName("A relation that names a table the DataSet does not have is refused")]
        public void DataSet_RelationWithMissingTable_Throws(string? parent, string? child)
        {
            var bytes = DataSetWithRelation((ref MessagePackWriter w) => WriteRelation(ref w, parent, child, "Id"));

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataSet>(bytes));
        }

        [Theory]
        [InlineData("Ghost")]
        [InlineData(null)]
        [DisplayName("A relation that names a column its table does not have is refused")]
        public void DataSet_RelationWithMissingColumn_Throws(string? childColumn)
        {
            var bytes = DataSetWithRelation((ref MessagePackWriter w) => WriteRelation(ref w, "P", "C", childColumn));

            Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataSet>(bytes));
        }
    }
}
