using System.Buffers;
using MessagePack;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// Reads and writes the MessagePack DataTable layout by hand, so tests can look at the wire itself and hand the
    /// reader payloads the writer would never produce.
    /// </summary>
    /// <remarks>
    /// This deliberately restates the layout documented on <c>DataTableFormatter</c> rather than calling it: a test
    /// that decoded the bytes with the formatter under test could not notice the layout changing.
    /// </remarks>
    internal static class DataTableWire
    {
        internal sealed record Column(string Name, int FieldDbType, string Caption, bool AllowDBNull, bool ReadOnly, int MaxLength);

        internal sealed record Row(int State, IReadOnlyList<byte[]> Cells);

        internal sealed record Table(string TableName, IReadOnlyList<Column> Columns, IReadOnlyList<string> PrimaryKey, IReadOnlyList<Row> Rows);

        /// <summary>
        /// Decodes a top-level DataTable body. Cells are returned as their raw MessagePack bytes.
        /// </summary>
        public static Table Read(byte[] bytes)
        {
            var reader = new MessagePackReader(new ReadOnlySequence<byte>(bytes));
            Assert.Equal(4, reader.ReadArrayHeader());
            var tableName = reader.ReadString()!;

            var columns = new List<Column>();
            var columnCount = reader.ReadArrayHeader();
            for (var i = 0; i < columnCount; i++)
            {
                Assert.Equal(7, reader.ReadArrayHeader());
                columns.Add(new Column(reader.ReadString()!, reader.ReadInt32(), reader.ReadString()!,
                    reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadInt32()));
                reader.Skip();
            }

            var primaryKey = new List<string>();
            var keyCount = reader.ReadArrayHeader();
            for (var i = 0; i < keyCount; i++)
                primaryKey.Add(reader.ReadString()!);

            var rows = new List<Row>();
            var rowCount = reader.ReadArrayHeader();
            for (var i = 0; i < rowCount; i++)
            {
                var length = reader.ReadArrayHeader();
                var state = reader.ReadInt32();
                var cells = new List<byte[]>();
                for (var c = 1; c < length; c++)
                    cells.Add(reader.ReadRaw().ToArray());
                rows.Add(new Row(state, cells));
            }

            Assert.True(reader.End, "The DataTable body has bytes after the table entry.");
            return new Table(tableName, columns, primaryKey, rows);
        }

        /// <summary>
        /// Decodes one raw string cell.
        /// </summary>
        public static string? ReadString(byte[] cell)
        {
            var reader = new MessagePackReader(new ReadOnlySequence<byte>(cell));
            return reader.ReadString();
        }

        /// <summary>
        /// Writes a table with <c>Int32</c> columns and no rows, with the primary key names given verbatim.
        /// </summary>
        public static byte[] WriteInt32Table(string tableName, string[] columnNames, string[] primaryKey)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(4);
            writer.Write(tableName);
            writer.WriteArrayHeader(columnNames.Length);
            foreach (var name in columnNames)
                WriteInt32Column(ref writer, name);
            writer.WriteArrayHeader(primaryKey.Length);
            foreach (var name in primaryKey)
                writer.Write(name);
            writer.WriteArrayHeader(0);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Writes a table with one <c>Int32</c> column and one row given as its state and raw cell values.
        /// </summary>
        public static byte[] WriteSingleRowTable(int state, params int[] cells)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(4);
            writer.Write("T");
            writer.WriteArrayHeader(1);
            WriteInt32Column(ref writer, "Id");
            writer.WriteArrayHeader(0);
            writer.WriteArrayHeader(1);
            writer.WriteArrayHeader(1 + cells.Length);
            writer.Write(state);
            foreach (var cell in cells)
                writer.Write(cell);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Writes a table whose only column declares the given raw field type value.
        /// </summary>
        public static byte[] WriteColumnWithFieldType(int fieldDbType)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(4);
            writer.Write("T");
            writer.WriteArrayHeader(1);
            WriteColumn(ref writer, "c", fieldDbType);
            writer.WriteArrayHeader(0);
            writer.WriteArrayHeader(0);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        private static void WriteInt32Column(ref MessagePackWriter writer, string name)
            => WriteColumn(ref writer, name, (int)Polhem.Core.Data.FieldDbType.Integer);

        private static void WriteColumn(ref MessagePackWriter writer, string name, int fieldDbType)
        {
            writer.WriteArrayHeader(7);
            writer.Write(name);
            writer.Write(fieldDbType);
            writer.Write(string.Empty);
            writer.Write(true);
            writer.Write(false);
            writer.Write(-1);
            writer.WriteNil();
        }
    }
}
