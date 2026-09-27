using System.ComponentModel;
using System.Data;
using System.Text;
using MessagePack;
using Polhem.Api.Core.MessagePack;
using Polhem.Base.Data;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// Pins the MessagePack DataTable layout itself: column names once, rows positional, original values only where a
    /// row needs them, and a reader that refuses entries whose length does not match their row state.
    /// </summary>
    public class DataTableWireShapeTests
    {
        private static DataTable BuildTable()
        {
            var table = new DataTable("t");
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("distinctive_column", typeof(string));
            table.Rows.Add(1, "unchanged");
            table.Rows.Add(2, "before");
            table.Rows.Add(3, "deleted");
            table.AcceptChanges();
            table.Rows[1]["distinctive_column"] = "after";
            table.Rows[2].Delete();
            table.Rows.Add(4, "added");
            return table;
        }

        private static int CountOccurrences(byte[] haystack, byte[] needle)
        {
            var count = 0;
            for (var i = haystack.AsSpan().IndexOf(needle); i >= 0;)
            {
                count++;
                var next = haystack.AsSpan(i + 1).IndexOf(needle);
                i = next < 0 ? -1 : i + 1 + next;
            }
            return count;
        }

        [Fact]
        [DisplayName("Column names are written in the column header only, so a hundred rows repeat them no more often than one row")]
        public void Serialize_ManyRows_WritesColumnNamesOnlyInTheHeader()
        {
            static int NameOccurrences(int rowCount)
            {
                var table = new DataTable("t");
                table.Columns.Add("distinctive_column", typeof(string)).Caption = "Caption";
                for (var i = 0; i < rowCount; i++)
                    table.Rows.Add("v" + i);
                table.AcceptChanges();
                return CountOccurrences(MessagePackCodec.Serialize(table), Encoding.UTF8.GetBytes("distinctive_column"));
            }

            Assert.Equal(1, NameOccurrences(1));
            Assert.Equal(1, NameOccurrences(100));
        }

        [Fact]
        [DisplayName("Unchanged, Added and Deleted rows carry one value per column; only a Modified row carries two")]
        public void Serialize_RowStates_WriteOneValueSetExceptModified()
        {
            var wire = DataTableWire.Read(MessagePackCodec.Serialize(BuildTable()));

            Assert.Equal(
                [((int)DataRowState.Unchanged, 2), ((int)DataRowState.Modified, 4), ((int)DataRowState.Deleted, 2), ((int)DataRowState.Added, 2)],
                wire.Rows.Select(r => (r.State, r.Cells.Count)));
        }

        [Fact]
        [DisplayName("A Modified row writes its original values first, then its current values")]
        public void Serialize_ModifiedRow_WritesOriginalThenCurrent()
        {
            var wire = DataTableWire.Read(MessagePackCodec.Serialize(BuildTable()));

            var modified = wire.Rows[1];
            Assert.Equal("before", DataTableWire.ReadString(modified.Cells[1]));
            Assert.Equal("after", DataTableWire.ReadString(modified.Cells[3]));
        }

        [Fact]
        [DisplayName("A Deleted row writes its original values")]
        public void Serialize_DeletedRow_WritesOriginalValues()
        {
            var wire = DataTableWire.Read(MessagePackCodec.Serialize(BuildTable()));

            Assert.Equal("deleted", DataTableWire.ReadString(wire.Rows[2].Cells[1]));
        }

        [Fact]
        [DisplayName("A cell is its bare value, typed by its column, and DBNull is nil")]
        public void Serialize_Cells_CarryNoDiscriminator()
        {
            var table = new DataTable("t");
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("name", typeof(string));
            table.Rows.Add(7, DBNull.Value);

            var row = Assert.Single(DataTableWire.Read(MessagePackCodec.Serialize(table)).Rows);

            Assert.Equal(new byte[] { 0x07 }, row.Cells[0]);
            Assert.Equal(new byte[] { MessagePackCode.Nil }, row.Cells[1]);
        }

        [Fact]
        [DisplayName("The column entry carries the declared field type, not only the CLR type")]
        public void Serialize_DateColumn_WritesDateFieldType()
        {
            var table = new DataTable("t");
            table.AddColumn("order_date", FieldDbType.Date);
            table.AddColumn("created_at", FieldDbType.DateTime);

            var wire = DataTableWire.Read(MessagePackCodec.Serialize(table));

            Assert.Equal([(int)FieldDbType.Date, (int)FieldDbType.DateTime], wire.Columns.Select(c => c.FieldDbType));
        }

        [Theory]
        [InlineData((int)DataRowState.Unchanged, new[] { 1, 2 })]
        [InlineData((int)DataRowState.Modified, new[] { 1 })]
        [InlineData((int)DataRowState.Deleted, new int[0])]
        [DisplayName("A row entry whose length does not match its state is rejected")]
        public void Deserialize_RowLengthMismatch_Throws(int state, int[] cells)
        {
            var bytes = DataTableWire.WriteSingleRowTable(state, cells);

            var ex = Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));

            Assert.Contains("expected", ex.ToString(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData((int)DataRowState.Detached)]
        [InlineData(3)]
        [DisplayName("A row entry with a state that cannot travel is rejected")]
        public void Deserialize_UnexpectedRowState_Throws(int state)
        {
            var bytes = DataTableWire.WriteSingleRowTable(state, 1);

            var ex = Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));

            Assert.Contains("Unexpected DataRow state", ex.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A column entry with an undefined field type is rejected")]
        public void Deserialize_UndefinedFieldDbType_Throws()
        {
            var bytes = DataTableWire.WriteColumnWithFieldType(999);

            var ex = Assert.ThrowsAny<MessagePackSerializationException>(() => MessagePackCodec.Deserialize<DataTable>(bytes));

            Assert.Contains("Unknown field type 999", ex.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A hand-written payload in the documented layout is read back with its row state")]
        public void Deserialize_DocumentedLayout_RestoresRow()
        {
            var bytes = DataTableWire.WriteSingleRowTable((int)DataRowState.Modified, 1, 2);

            var table = MessagePackCodec.Deserialize<DataTable>(bytes)!;

            var row = Assert.Single(table.Rows.Cast<DataRow>());
            Assert.Equal(DataRowState.Modified, row.RowState);
            Assert.Equal(1, row["Id", DataRowVersion.Original]);
            Assert.Equal(2, row["Id"]);
        }
    }
}
