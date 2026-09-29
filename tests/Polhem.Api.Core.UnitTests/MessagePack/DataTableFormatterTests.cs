using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Polhem.Api.Core.MessagePack;
using Polhem.Core.Data;
using Polhem.Core.Serialization;

namespace Polhem.Api.Core.UnitTests.MessagePack
{
    /// <summary>
    /// Round-trip tests for the MessagePack DataTable shape: row states, DBNull, every <see cref="FieldDbType"/>, empty
    /// tables and the column metadata.
    /// </summary>
    public class DataTableFormatterTests
    {
        private static DataTable RoundTrip(DataTable table)
            => MessagePackCodec.Deserialize<DataTable>(MessagePackCodec.Serialize(table))!;

        /// <summary>
        /// One representative value per field type, chosen at the edges a lossy encoding would get wrong.
        /// </summary>
        private static object SampleValue(FieldDbType fieldDbType) => fieldDbType switch
        {
            FieldDbType.String => "héllo, 世界",
            FieldDbType.Text => new string('x', 5000),
            FieldDbType.Time => "23:59",
            FieldDbType.Boolean => true,
            FieldDbType.AutoIncrement => int.MaxValue,
            FieldDbType.Short => short.MinValue,
            FieldDbType.Integer => int.MinValue,
            FieldDbType.Long => long.MaxValue,
            // Trailing zeros are part of a decimal's value (its scale) and must survive.
            FieldDbType.Decimal => 79228162514264337593543950335m,
            FieldDbType.Currency => -12.3400m,
            FieldDbType.Date => new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Unspecified),
            FieldDbType.DateTime => new DateTime(2026, 7, 25, 13, 14, 15, DateTimeKind.Unspecified).AddTicks(1234567),
            FieldDbType.Guid => new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
            FieldDbType.Binary => new byte[] { 0, 1, 2, 254, 255 },
            _ => throw new ArgumentOutOfRangeException(nameof(fieldDbType), fieldDbType, null),
        };

        public static TheoryData<FieldDbType> FieldDbTypesWithClrType()
        {
            var data = new TheoryData<FieldDbType>();
            foreach (var value in Enum.GetValues<FieldDbType>().Where(t => t != FieldDbType.Unknown))
                data.Add(value);
            return data;
        }

        [Theory]
        [MemberData(nameof(FieldDbTypesWithClrType))]
        [DisplayName("A cell of every field type round-trips exactly, and a DBNull cell of that type stays DBNull")]
        public void RoundTrip_EveryFieldDbType_PreservesValueAndDbNull(FieldDbType fieldDbType)
        {
            var table = new DataTable("t");
            table.AddColumn("c", fieldDbType).AllowDBNull = true;
            var value = SampleValue(fieldDbType);
            table.Rows.Add(value);
            table.Rows.Add(DBNull.Value);
            table.AcceptChanges();

            var restored = RoundTrip(table);

            var column = restored.Columns["c"]!;
            Assert.Equal(DbTypeConverter.ToType(fieldDbType), column.DataType);
            Assert.Equal(fieldDbType, column.ResolveFieldDbType());
            var actual = restored.Rows[0]["c"];
            Assert.IsType(value.GetType(), actual);
            Assert.Equal(value, actual);
            if (value is decimal d)
                Assert.Equal(d.ToString(CultureInfo.InvariantCulture), ((decimal)actual).ToString(CultureInfo.InvariantCulture));
            if (value is DateTime dt)
                Assert.Equal((dt.Ticks, DateTimeKind.Unspecified), (((DateTime)actual).Ticks, ((DateTime)actual).Kind));
            Assert.True(restored.Rows[1].IsNull("c"));
        }

        [Fact]
        [DisplayName("A column declared Unknown has no CLR type and fails when the table is serialized")]
        public void Serialize_ColumnDeclaredUnknown_Throws()
        {
            var table = new DataTable("t");
            table.Columns.Add("c", typeof(string)).ApplyFieldDbType(FieldDbType.Unknown);

            var ex = Assert.ThrowsAny<Exception>(() => MessagePackCodec.Serialize(table));

            Assert.Contains("Unknown", ex.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A value whose CLR type differs from the rebuilt column type arrives converted to the column type")]
        public void RoundTrip_DoubleColumn_ArrivesAsDecimal()
        {
            var table = new DataTable("t");
            table.Columns.Add("rate", typeof(double));
            table.Rows.Add(1.25d);

            var restored = RoundTrip(table);

            Assert.Equal(typeof(decimal), restored.Columns["rate"]!.DataType);
            Assert.Equal(1.25m, restored.Rows[0]["rate"]);
        }

        [Fact]
        [DisplayName("A GUID held as a string in a column declared Guid, as a SQLite reader returns it, arrives as a Guid")]
        public void RoundTrip_GuidStringInGuidColumn_ArrivesAsGuid()
        {
            var value = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
            var table = new DataTable("t");
            table.Columns.Add("rowid", typeof(string)).ApplyFieldDbType(FieldDbType.Guid);
            table.Rows.Add(value.ToString());

            var restored = RoundTrip(table);

            Assert.Equal(typeof(Guid), restored.Columns["rowid"]!.DataType);
            Assert.Equal(value, restored.Rows[0]["rowid"]);
        }

        [Fact]
        [DisplayName("Added, Modified, Deleted and Unchanged rows keep their order, state, current values and original values")]
        public void RoundTrip_AllRowStates_PreservesStatesAndVersions()
        {
            var table = new DataTable("t");
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("name", typeof(string));
            table.Rows.Add(1, "unchanged");
            table.Rows.Add(2, "before edit");
            table.Rows.Add(3, "to delete");
            table.AcceptChanges();
            table.Rows[1]["name"] = "after edit";
            table.Rows[2].Delete();
            table.Rows.Add(4, "added");

            var restored = RoundTrip(table);

            Assert.Equal(
                [DataRowState.Unchanged, DataRowState.Modified, DataRowState.Deleted, DataRowState.Added],
                restored.Rows.Cast<DataRow>().Select(r => r.RowState));
            Assert.Equal("unchanged", restored.Rows[0]["name"]);
            Assert.Equal("unchanged", restored.Rows[0]["name", DataRowVersion.Original]);
            Assert.Equal("after edit", restored.Rows[1]["name"]);
            Assert.Equal("before edit", restored.Rows[1]["name", DataRowVersion.Original]);
            Assert.Equal(3, restored.Rows[2]["id", DataRowVersion.Original]);
            Assert.Equal("to delete", restored.Rows[2]["name", DataRowVersion.Original]);
            Assert.Equal("added", restored.Rows[3]["name"]);
            Assert.False(restored.Rows[3].HasVersion(DataRowVersion.Original));
        }

        [Fact]
        [DisplayName("A detached row is not part of the table and does not travel")]
        public void RoundTrip_DetachedRows_AreNotSerialized()
        {
            var table = new DataTable("t");
            table.Columns.Add("id", typeof(int));
            table.Rows.Add(1);
            var removed = table.Rows.Add(2);
            table.Rows.Remove(removed);
            var neverAdded = table.NewRow();
            neverAdded["id"] = 3;

            var restored = RoundTrip(table);

            Assert.Equal(DataRowState.Detached, removed.RowState);
            Assert.Equal(DataRowState.Detached, neverAdded.RowState);
            var row = Assert.Single(restored.Rows.Cast<DataRow>());
            Assert.Equal(1, row["id"]);
        }

        [Fact]
        [DisplayName("DBNull survives in the current and original values of every row state")]
        public void RoundTrip_DbNullCells_PreservedInEveryRowState()
        {
            var table = new DataTable("t");
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("note", typeof(string));
            table.Rows.Add(1, DBNull.Value);
            table.Rows.Add(2, DBNull.Value);
            table.Rows.Add(3, "was set");
            table.Rows.Add(4, DBNull.Value);
            table.AcceptChanges();
            table.Rows[1]["note"] = "now set";
            table.Rows[2]["note"] = DBNull.Value;
            table.Rows[3].Delete();
            table.Rows.Add(5, DBNull.Value);

            var restored = RoundTrip(table);

            Assert.True(restored.Rows[0].IsNull("note"));
            Assert.Equal("now set", restored.Rows[1]["note"]);
            Assert.True(restored.Rows[1].IsNull(restored.Columns["note"]!, DataRowVersion.Original));
            Assert.True(restored.Rows[2].IsNull("note"));
            Assert.Equal("was set", restored.Rows[2]["note", DataRowVersion.Original]);
            Assert.True(restored.Rows[3].IsNull(restored.Columns["note"]!, DataRowVersion.Original));
            Assert.True(restored.Rows[4].IsNull("note"));
        }

        [Fact]
        [DisplayName("An empty table without columns keeps its name")]
        public void RoundTrip_EmptyTableWithoutColumns_PreservesName()
        {
            var restored = RoundTrip(new DataTable("nothing"));

            Assert.Equal("nothing", restored.TableName);
            Assert.Empty(restored.Columns);
            Assert.Empty(restored.Rows);
        }

        [Fact]
        [DisplayName("A table without columns keeps its rows and their states")]
        public void RoundTrip_RowsWithoutColumns_PreservesRowStates()
        {
            var table = new DataTable("t");
            table.Rows.Add();
            table.AcceptChanges();
            table.Rows.Add();

            var restored = RoundTrip(table);

            Assert.Equal(
                [DataRowState.Unchanged, DataRowState.Added],
                restored.Rows.Cast<DataRow>().Select(r => r.RowState));
        }

        [Fact]
        [DisplayName("Column metadata survives: name, type marker, caption, AllowDBNull, ReadOnly, MaxLength, DefaultValue, DateTimeMode and the primary key")]
        public void RoundTrip_ColumnMetadata_IsPreserved()
        {
            var table = new DataTable("orders");
            var id = table.AddColumn("id", FieldDbType.Integer);
            var code = table.AddColumn("code", "Order code", FieldDbType.String, "N/A");
            code.MaxLength = 20;
            code.AllowDBNull = false;
            var orderDate = table.AddColumn("order_date", FieldDbType.Date);
            orderDate.AllowDBNull = true;
            var createdAt = table.AddColumn("created_at", FieldDbType.DateTime);
            var locked = table.AddColumn("locked", FieldDbType.Boolean);
            locked.ReadOnly = true;
            table.Columns.Add("remark", typeof(string));
            table.PrimaryKey = [id, code];

            var restored = RoundTrip(table);

            Assert.Equal("orders", restored.TableName);
            Assert.Equal(["id", "code"], restored.PrimaryKey.Select(c => c.ColumnName));
            foreach (DataColumn source in table.Columns)
            {
                var actual = restored.Columns[source.ColumnName]!;
                Assert.Equal(source.Ordinal, actual.Ordinal);
                Assert.Equal(source.DataType, actual.DataType);
                Assert.Equal(source.ResolveFieldDbType(), actual.ResolveFieldDbType());
                Assert.Equal(source.Caption, actual.Caption);
                Assert.Equal(source.AllowDBNull, actual.AllowDBNull);
                Assert.Equal(source.ReadOnly, actual.ReadOnly);
                Assert.Equal(source.MaxLength, actual.MaxLength);
                Assert.Equal(source.DefaultValue, actual.DefaultValue);
            }
            Assert.Equal(FieldDbType.Date, restored.Columns["order_date"]!.ResolveFieldDbType());
            Assert.Equal(DataSetDateTime.Unspecified, restored.Columns["order_date"]!.DateTimeMode);
            Assert.Equal(DataSetDateTime.Unspecified, restored.Columns["created_at"]!.DateTimeMode);
            Assert.Equal(DBNull.Value, restored.Columns["remark"]!.DefaultValue);
        }

        [Fact]
        [DisplayName("A read-only column does not stop a Modified row from being restored, and stays read-only")]
        public void RoundTrip_ReadOnlyColumnInModifiedRow_RestoresRowAndFlag()
        {
            var table = new DataTable("t");
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("name", typeof(string));
            table.Rows.Add(1, "old");
            table.AcceptChanges();
            table.Rows[0]["name"] = "new";
            table.Columns["id"]!.ReadOnly = true;

            var restored = RoundTrip(table);

            Assert.True(restored.Columns["id"]!.ReadOnly);
            Assert.Equal(DataRowState.Modified, restored.Rows[0].RowState);
            Assert.Equal(1, restored.Rows[0]["id"]);
            Assert.Equal("old", restored.Rows[0]["name", DataRowVersion.Original]);
            Assert.Equal("new", restored.Rows[0]["name"]);
        }

        [Fact]
        [DisplayName("The MessagePack and JSON codecs rebuild the same columns, primary key, row states and values")]
        public void BothCodecs_RebuildTheSameTable()
        {
            var table = new DataTable("orders");
            var id = table.AddColumn("id", FieldDbType.Integer);
            table.AddColumn("code", "Order code", FieldDbType.String, "N/A").MaxLength = 20;
            table.AddColumn("order_date", FieldDbType.Date).AllowDBNull = true;
            table.AddColumn("amount", FieldDbType.Currency);
            table.AddColumn("rowid", FieldDbType.Guid);
            table.PrimaryKey = [id];
            table.Rows.Add(1, "A", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), 10.50m, Guid.NewGuid());
            table.Rows.Add(2, "B", DBNull.Value, 0m, Guid.NewGuid());
            table.Rows.Add(3, "C", DBNull.Value, 3m, Guid.NewGuid());
            table.AcceptChanges();
            table.Rows[1]["amount"] = 99.99m;
            table.Rows[2].Delete();
            table.Rows.Add(4, "D", DBNull.Value, 1m, Guid.NewGuid());

            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new DataTableJsonConverter());
            var viaMessagePack = RoundTrip(table);
            var viaJson = JsonSerializer.Deserialize<DataTable>(JsonSerializer.Serialize(table, jsonOptions), jsonOptions)!;

            Assert.Equal(viaJson.TableName, viaMessagePack.TableName);
            Assert.Equal(viaJson.PrimaryKey.Select(c => c.ColumnName), viaMessagePack.PrimaryKey.Select(c => c.ColumnName));
            Assert.Equal(viaJson.Columns.Count, viaMessagePack.Columns.Count);
            foreach (DataColumn expected in viaJson.Columns)
            {
                var actual = viaMessagePack.Columns[expected.ColumnName]!;
                Assert.Equal(
                    (expected.Ordinal, expected.DataType, expected.ResolveFieldDbType(), expected.Caption, expected.AllowDBNull,
                        expected.ReadOnly, expected.MaxLength, expected.DefaultValue, expected.DateTimeMode),
                    (actual.Ordinal, actual.DataType, actual.ResolveFieldDbType(), actual.Caption, actual.AllowDBNull,
                        actual.ReadOnly, actual.MaxLength, actual.DefaultValue, actual.DateTimeMode));
            }
            Assert.Equal(viaJson.Rows.Count, viaMessagePack.Rows.Count);
            for (var r = 0; r < viaJson.Rows.Count; r++)
            {
                var expected = viaJson.Rows[r];
                var actual = viaMessagePack.Rows[r];
                Assert.Equal(expected.RowState, actual.RowState);
                foreach (var version in new[] { DataRowVersion.Current, DataRowVersion.Original })
                {
                    Assert.Equal(expected.HasVersion(version), actual.HasVersion(version));
                    if (!expected.HasVersion(version)) continue;
                    foreach (DataColumn column in viaJson.Columns)
                        Assert.Equal(expected[column.ColumnName, version], actual[column.ColumnName, version]);
                }
            }
        }
    }
}
