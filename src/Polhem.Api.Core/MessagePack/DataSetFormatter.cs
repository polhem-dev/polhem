using System.Data;
using MessagePack;
using MessagePack.Formatters;

namespace Polhem.Api.Core.MessagePack
{
    /// <summary>
    /// MessagePack formatter for <see cref="DataSet"/>: its tables in the <see cref="DataTableFormatter"/> shape,
    /// followed by its relations.
    /// </summary>
    /// <remarks>
    /// The layout, every level a MessagePack array:
    /// <code>
    /// dataSet  = [dataSetName, [table...], [relation...]]
    /// relation = [relationName, parentTableName, childTableName, [parentColumnName...], [childColumnName...]]
    /// </code>
    /// The reader skips extra trailing elements in a data set or relation entry, as the table reader does.
    /// </remarks>
    internal sealed class DataSetFormatter : IMessagePackFormatter<DataSet?>
    {
        private const int DataSetFieldCount = 3;
        private const int RelationFieldCount = 5;

        /// <summary>
        /// Serializes the DataSet.
        /// </summary>
        public void Serialize(ref MessagePackWriter writer, DataSet? value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(DataSetFieldCount);
            writer.Write(value.DataSetName);

            writer.WriteArrayHeader(value.Tables.Count);
            foreach (DataTable table in value.Tables)
                DataTableFormatter.WriteTable(ref writer, table, options);

            writer.WriteArrayHeader(value.Relations.Count);
            foreach (DataRelation relation in value.Relations)
            {
                writer.WriteArrayHeader(RelationFieldCount);
                writer.Write(relation.RelationName);
                writer.Write(relation.ParentTable.TableName);
                writer.Write(relation.ChildTable.TableName);
                WriteColumnNames(ref writer, relation.ParentColumns);
                WriteColumnNames(ref writer, relation.ChildColumns);
            }
        }

        /// <summary>
        /// Deserializes the DataSet.
        /// </summary>
        public DataSet? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            options.Security.DepthStep(ref reader);
            try
            {
                var fieldCount = reader.ReadArrayHeader();
                if (fieldCount < DataSetFieldCount)
                    throw new MessagePackSerializationException($"Unexpected DataSet entry length {fieldCount}.");

                var dataSet = new DataSet(reader.ReadString() ?? string.Empty);

                var tableCount = reader.ReadArrayHeader();
                for (var i = 0; i < tableCount; i++)
                    dataSet.Tables.Add(DataTableFormatter.ReadTable(ref reader, options));

                var relationCount = reader.ReadArrayHeader();
                for (var i = 0; i < relationCount; i++)
                    dataSet.Relations.Add(ReadRelation(ref reader, dataSet));

                for (var i = DataSetFieldCount; i < fieldCount; i++)
                    reader.Skip();

                return dataSet;
            }
            finally
            {
                reader.Depth--;
            }
        }

        private static void WriteColumnNames(ref MessagePackWriter writer, DataColumn[] columns)
        {
            writer.WriteArrayHeader(columns.Length);
            foreach (var column in columns)
                writer.Write(column.ColumnName);
        }

        private static DataRelation ReadRelation(ref MessagePackReader reader, DataSet dataSet)
        {
            var fieldCount = reader.ReadArrayHeader();
            if (fieldCount < RelationFieldCount)
                throw new MessagePackSerializationException($"Unexpected DataRelation entry length {fieldCount}.");

            var relationName = reader.ReadString() ?? string.Empty;
            var parentTable = FindTable(dataSet, reader.ReadString(), relationName);
            var childTable = FindTable(dataSet, reader.ReadString(), relationName);
            var parentColumns = ReadColumns(ref reader, parentTable, relationName);
            var childColumns = ReadColumns(ref reader, childTable, relationName);

            for (var i = RelationFieldCount; i < fieldCount; i++)
                reader.Skip();

            return new DataRelation(relationName, parentColumns, childColumns);
        }

        private static DataTable FindTable(DataSet dataSet, string? tableName, string relationName)
            => (tableName == null ? null : dataSet.Tables[tableName])
                ?? throw new MessagePackSerializationException($"Relation '{relationName}' names a table the DataSet does not have.");

        private static DataColumn[] ReadColumns(ref MessagePackReader reader, DataTable table, string relationName)
        {
            var count = reader.ReadArrayHeader();
            var columns = new DataColumn[count];
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                columns[i] = (name == null ? null : table.Columns[name])
                    ?? throw new MessagePackSerializationException($"Relation '{relationName}' names a column table '{table.TableName}' does not have.");
            }
            return columns;
        }
    }
}
