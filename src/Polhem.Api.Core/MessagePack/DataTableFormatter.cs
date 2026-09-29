using System.Data;
using MessagePack;
using MessagePack.Formatters;
using Polhem.Core.Data;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// MessagePack formatter for <see cref="DataTable"/>: a column table written once, then each row as a positional
    /// array.
    /// </summary>
    /// <remarks>
    /// The layout, every level a MessagePack array:
    /// <code>
    /// table  = [tableName, [column...], [primaryKeyColumnName...], [row...]]
    /// column = [columnName, fieldDbType, caption, allowDBNull, readOnly, maxLength, defaultValue]
    /// row    = [Unchanged, current...]            1 + n elements
    ///        | [Added,     current...]            1 + n elements
    ///        | [Deleted,   original...]           1 + n elements
    ///        | [Modified,  original..., current...]  1 + 2n elements
    /// </code>
    /// <c>n</c> is the column count, the row state is the <see cref="DataRowState"/> value as an integer, and cells
    /// are in column order, typed by their column (<see cref="DataColumnCodec"/>). Only a Modified row carries both
    /// versions: an Unchanged row's two versions are equal by definition, an Added row has no original, and a
    /// Deleted row has only its original. Detached rows are never in <see cref="DataTable.Rows"/>.
    /// <para>
    /// The reader accepts extra trailing elements in a table or column entry and skips them, so a later version can
    /// append metadata without breaking this one. The column metadata is the set the JSON shape
    /// (<see cref="Polhem.Core.Serialization.DataTableJsonConverter"/>) carries, and
    /// <c>DataTableFormatterTests.BothCodecs_RebuildTheSameTable</c> compares the tables the two rebuild.
    /// </para>
    /// <para>
    /// NOTE: This replaced a shape that wrote every row as a map of column name to a <c>[code, value]</c> envelope,
    /// through an intermediate object graph with one dictionary per row. Repeating the column names was about half
    /// of the bytes of a typical list response.
    /// </para>
    /// </remarks>
    internal sealed class DataTableFormatter : IMessagePackFormatter<DataTable?>
    {
        private const int TableFieldCount = 4;
        private const int ColumnFieldCount = 7;

        /// <summary>
        /// Serializes the DataTable.
        /// </summary>
        public void Serialize(ref MessagePackWriter writer, DataTable? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            WriteTable(ref writer, value, options);
        }

        /// <summary>
        /// Deserializes the DataTable.
        /// </summary>
        public DataTable? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            options.Security.DepthStep(ref reader);
            try
            {
                return ReadTable(ref reader, options);
            }
            finally
            {
                reader.Depth--;
            }
        }

        /// <summary>
        /// Writes a non-null table.
        /// </summary>
        internal static void WriteTable(ref MessagePackWriter writer, DataTable table, MessagePackSerializerOptions options)
        {
            var columns = new DataColumn[table.Columns.Count];
            table.Columns.CopyTo(columns, 0);
            var codecs = new DataColumnCodec[columns.Length];

            writer.WriteArrayHeader(TableFieldCount);
            writer.Write(table.TableName);

            writer.WriteArrayHeader(columns.Length);
            for (var i = 0; i < columns.Length; i++)
            {
                var column = columns[i];
                var fieldDbType = column.ResolveFieldDbType();
                codecs[i] = DataColumnCodec.For(fieldDbType, options);

                writer.WriteArrayHeader(ColumnFieldCount);
                writer.Write(column.ColumnName);
                writer.Write((int)fieldDbType);
                writer.Write(column.Caption);
                writer.Write(column.AllowDBNull);
                writer.Write(column.ReadOnly);
                writer.Write(column.MaxLength);
                codecs[i].Write(ref writer, column.DefaultValue, options);
            }

            var primaryKey = table.PrimaryKey;
            writer.WriteArrayHeader(primaryKey.Length);
            foreach (var column in primaryKey)
                writer.Write(column.ColumnName);

            var rows = table.Rows;
            writer.WriteArrayHeader(rows.Count);
            foreach (DataRow row in rows)
                WriteRow(ref writer, row, columns, codecs, options);
        }

        private static void WriteRow(ref MessagePackWriter writer, DataRow row, DataColumn[] columns, DataColumnCodec[] codecs, MessagePackSerializerOptions options)
        {
            var state = row.RowState;
            switch (state)
            {
                case DataRowState.Unchanged:
                case DataRowState.Added:
                    writer.WriteArrayHeader(1 + columns.Length);
                    writer.Write((int)state);
                    WriteValues(ref writer, row, DataRowVersion.Current, columns, codecs, options);
                    break;

                case DataRowState.Deleted:
                    writer.WriteArrayHeader(1 + columns.Length);
                    writer.Write((int)state);
                    WriteValues(ref writer, row, DataRowVersion.Original, columns, codecs, options);
                    break;

                case DataRowState.Modified:
                    writer.WriteArrayHeader(1 + (2 * columns.Length));
                    writer.Write((int)state);
                    WriteValues(ref writer, row, DataRowVersion.Original, columns, codecs, options);
                    WriteValues(ref writer, row, DataRowVersion.Current, columns, codecs, options);
                    break;

                default:
                    // A row in `DataTable.Rows` is never Detached; the row count above has already been written.
                    throw new InvalidOperationException($"Cannot serialize a row in state '{state}'.");
            }
        }

        private static void WriteValues(ref MessagePackWriter writer, DataRow row, DataRowVersion version, DataColumn[] columns, DataColumnCodec[] codecs, MessagePackSerializerOptions options)
        {
            for (var i = 0; i < columns.Length; i++)
                codecs[i].Write(ref writer, row[columns[i], version], options);
        }

        /// <summary>
        /// Reads a non-nil table.
        /// </summary>
        internal static DataTable ReadTable(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var fieldCount = reader.ReadArrayHeader();
            if (fieldCount < TableFieldCount)
                throw new MessagePackSerializationException($"Unexpected DataTable entry length {fieldCount}.");

            var table = new DataTable(reader.ReadString() ?? string.Empty);

            var columnCount = reader.ReadArrayHeader();
            var codecs = new DataColumnCodec[columnCount];
            var readOnly = new bool[columnCount];
            for (var i = 0; i < columnCount; i++)
                ReadColumn(ref reader, table, out codecs[i], out readOnly[i], options);

            ReadPrimaryKey(ref reader, table);

            // One buffer for every row: `DataRow.ItemArray` copies the values into the table's own storage.
            var values = new object[columnCount];
            var rowCount = reader.ReadArrayHeader();
            for (var i = 0; i < rowCount; i++)
                ReadRow(ref reader, table, codecs, values, options);

            for (var i = TableFieldCount; i < fieldCount; i++)
                reader.Skip();

            // ReadOnly is applied after the rows: restoring a Modified row writes its current values into a row
            // that is already in the table, which a read-only column would refuse.
            for (var i = 0; i < columnCount; i++)
                table.Columns[i].ReadOnly = readOnly[i];

            return table;
        }

        private static void ReadColumn(ref MessagePackReader reader, DataTable table, out DataColumnCodec codec, out bool readOnly, MessagePackSerializerOptions options)
        {
            var fieldCount = reader.ReadArrayHeader();
            if (fieldCount < ColumnFieldCount)
                throw new MessagePackSerializationException($"Unexpected DataColumn entry length {fieldCount}.");

            var columnName = reader.ReadString() ?? string.Empty;
            var fieldDbType = (FieldDbType)reader.ReadInt32();
            if (!Enum.IsDefined(fieldDbType))
                throw new MessagePackSerializationException($"Unknown field type {(int)fieldDbType} for column '{columnName}'.");
            codec = DataColumnCodec.For(fieldDbType, options);

            var column = new DataColumn(columnName, codec.ClrType)
            {
                Caption = reader.ReadString() ?? string.Empty,
                AllowDBNull = reader.ReadBoolean(),
            };
            readOnly = reader.ReadBoolean();
            column.MaxLength = reader.ReadInt32();
            column.DefaultValue = codec.Read(ref reader, options);

            for (var i = ColumnFieldCount; i < fieldCount; i++)
                reader.Skip();

            // The .NET default for a fresh DateTime column is `UnspecifiedLocal`, the one mode that writes a
            // time-zone offset into XML. A rebuilt table must match the tables the framework builds itself (see
            // `DataTableExtensions.AddColumn`), or a payload that survived the wire intact would still shift once
            // persisted as XML.
            if (column.DataType == typeof(DateTime)) { column.DateTimeMode = DataSetDateTime.Unspecified; }
            // Several FieldDbType values share one CLR type, so the wire value carries information the rebuilt
            // DataColumn.DataType cannot. Record it so the client side stays as self-describing as the payload was.
            column.ApplyFieldDbType(fieldDbType);
            table.Columns.Add(column);
        }

        private static void ReadPrimaryKey(ref MessagePackReader reader, DataTable table)
        {
            var count = reader.ReadArrayHeader();
            var primaryKey = new List<DataColumn>(count);
            for (var i = 0; i < count; i++)
            {
                // A name the table does not have is dropped rather than failing the payload, as the JSON shape does.
                var name = reader.ReadString();
                if (name != null && table.Columns[name] is { } column)
                    primaryKey.Add(column);
            }

            if (primaryKey.Count > 0)
                table.PrimaryKey = primaryKey.ToArray();
        }

        private static void ReadRow(ref MessagePackReader reader, DataTable table, DataColumnCodec[] codecs, object[] values, MessagePackSerializerOptions options)
        {
            var length = reader.ReadArrayHeader();
            if (length < 1)
                throw new MessagePackSerializationException("A DataRow entry has no row state.");

            var state = (DataRowState)reader.ReadInt32();
            var expected = state switch
            {
                DataRowState.Unchanged or DataRowState.Added or DataRowState.Deleted => 1 + codecs.Length,
                DataRowState.Modified => 1 + (2 * codecs.Length),
                _ => throw new MessagePackSerializationException($"Unexpected DataRow state {(int)state}."),
            };
            if (length != expected)
                throw new MessagePackSerializationException($"A {state} DataRow entry has {length} elements; expected {expected}.");

            var row = table.NewRow();
            ReadValues(ref reader, row, codecs, values, options);
            table.Rows.Add(row);

            switch (state)
            {
                case DataRowState.Unchanged:
                    row.AcceptChanges();
                    break;

                case DataRowState.Deleted:
                    row.AcceptChanges();
                    row.Delete();
                    break;

                case DataRowState.Modified:
                    // The first set was the original version; accepting it makes it the Original, and the second
                    // set becomes Current.
                    row.AcceptChanges();
                    ReadValues(ref reader, row, codecs, values, options);
                    break;
            }
        }

        private static void ReadValues(ref MessagePackReader reader, DataRow row, DataColumnCodec[] codecs, object[] values, MessagePackSerializerOptions options)
        {
            for (var i = 0; i < codecs.Length; i++)
                values[i] = codecs[i].Read(ref reader, options);
            row.ItemArray = values;
        }
    }
}
