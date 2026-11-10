using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Polhem.Core.Data;
using Polhem.Core.Serialization;

namespace Polhem.Core.UnitTests.Serialization
{
    /// <summary>
    /// Fidelity tests for a <see cref="DataTable"/> round-trip over the JSON wire.
    /// </summary>
    /// <remarks>
    /// These tests check that values stay the same, not merely that nothing throws, because both distortions are
    /// silent: a string column rewritten as a date, and a decimal beyond double precision being truncated.
    /// </remarks>
    public class DataTableJsonFidelityTests
    {
        // One shared instance: `JsonSerializerOptions` is expensive to build and is frozen after its first use,
        // and every test here needs the same configuration.
        private static readonly JsonSerializerOptions s_options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new DataTableJsonConverter());
            return options;
        }

        private static DataTable RoundTrip(DataTable source)
        {

            var json = JsonSerializer.Serialize(source, s_options);
            return JsonSerializer.Deserialize<DataTable>(json, s_options)!;
        }

        [Theory]
        [InlineData("2026-07-28")]
        [InlineData("2026-07-28T10:30:00")]
        [InlineData("10:30")]
        [DisplayName("Date-shaped text in a string column survives a round-trip unchanged")]
        public void StringColumn_DateShapedText_SurvivesRoundTrip(string text)
        {
            var table = new DataTable("T");
            table.Columns.Add("note", typeof(string));
            table.Rows.Add(text);

            var restored = RoundTrip(table);

            // Previously `TryGetDateTime` succeeded first and `Convert.ChangeType` turned the value back into a
            // string, so "2026-07-28" became "07/28/2026 00:00:00".
            Assert.Equal(text, restored.Rows[0]["note"]);
        }

        [Fact]
        [DisplayName("A decimal column beyond double precision round-trips without losing precision")]
        public void DecimalColumn_HighPrecision_SurvivesRoundTrip()
        {
            // 17 significant digits. A double holds only about 15 to 16, so going through double would truncate it.
            const decimal amount = 12345678901234.567m;

            var table = new DataTable("T");
            table.Columns.Add("amount", typeof(decimal));
            table.Rows.Add(amount);

            var restored = RoundTrip(table);

            Assert.Equal(amount, restored.Rows[0]["amount"]);
        }

        [Fact]
        [DisplayName("A DateTime column is still restored as DateTime")]
        public void DateTimeColumn_StillParsesAsDateTime()
        {
            var value = new DateTime(2026, 7, 28, 10, 30, 0, DateTimeKind.Unspecified);

            var table = new DataTable("T");
            table.Columns.Add("created_at", typeof(DateTime));
            table.Rows.Add(value);

            var restored = RoundTrip(table);

            Assert.Equal(value, restored.Rows[0]["created_at"]);
        }
    
        [Theory]
        [InlineData("79228162514264337593543950335")]   // decimal.MaxValue
        [InlineData("-79228162514264337593543950335")]
        [InlineData("0.0000000000000000000000000001")]   // Smallest step.
        [InlineData("1234.56")]
        [DisplayName("A decimal column is carried as a JSON string and restored exactly, even beyond double precision")]
        public void DecimalColumn_BeyondDoublePrecision_SurvivesRoundTrip(string literal)
        {
            var expected = decimal.Parse(literal, CultureInfo.InvariantCulture);
            var table = new DataTable("T");
            table.Columns.Add("amount", typeof(decimal));
            table.Rows.Add(expected);

            var json = JsonSerializer.Serialize(table, s_options);

            // The shape is part of the contract. A bare number is a double to a JavaScript reader, so the value
            // would already be distorted before client code sees it.
            Assert.Contains($"\"amount\": \"{literal}\"".Replace(" ", string.Empty),
                json.Replace(" ", string.Empty), StringComparison.Ordinal);

            var restored = JsonSerializer.Deserialize<DataTable>(json, s_options)!;
            Assert.Equal(expected, Assert.IsType<decimal>(restored.Rows[0]["amount"]));
        }

        [Theory]
        [InlineData(9007199254740993L)]      // 2^53 + 1, which a double cannot hold.
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        [DisplayName("A long column beyond 2^53 is carried as a string and restored exactly")]
        public void Int64Column_BeyondSafeInteger_SurvivesRoundTrip(long expected)
        {
            var table = new DataTable("T");
            table.Columns.Add("bigint", typeof(long));
            table.Rows.Add(expected);

            var json = JsonSerializer.Serialize(table, s_options);

            Assert.Contains($"\"bigint\":\"{expected.ToString(CultureInfo.InvariantCulture)}\"",
                json.Replace(" ", string.Empty), StringComparison.Ordinal);

            var restored = JsonSerializer.Deserialize<DataTable>(json, s_options)!;
            Assert.Equal(expected, Assert.IsType<long>(restored.Rows[0]["bigint"]));
        }

        [Fact]
        [DisplayName("A bare number in the earlier format still reads back (the writer changed, the reader stays compatible)")]
        public void UnquotedNumericCell_FromEarlierRelease_StillReads()
        {
            // Releases before 4.27.0, and cross-language clients written against the wire-fixtures of that time,
            // send this shape.
            const string json = """
                {
                  "tableName": "T",
                  "columns": [
                    { "name": "amount", "type": "Decimal", "allowNull": true, "readOnly": false,
                      "maxLength": -1, "caption": "amount", "defaultValue": null }
                  ],
                  "primaryKeys": [],
                  "rows": [ { "state": "Unchanged", "current": { "amount": 12.5 } } ]
                }
                """;

            var restored = JsonSerializer.Deserialize<DataTable>(json, s_options)!;

            Assert.Equal(12.5m, Assert.IsType<decimal>(restored.Rows[0]["amount"]));
        }

        [Fact]
        [DisplayName("A non-numeric string in a decimal column does not silently become the default value")]
        public void DecimalColumn_NonNumericText_DoesNotSilentlyBecomeDefault()
        {
            const string json = """
                {
                  "tableName": "T",
                  "columns": [
                    { "name": "amount", "type": "Decimal", "allowNull": true, "readOnly": false,
                      "maxLength": -1, "caption": "amount", "defaultValue": null }
                  ],
                  "primaryKeys": [],
                  "rows": [ { "state": "Unchanged", "current": { "amount": "not a number" } } ]
                }
                """;


            // An unparseable string is passed on as is, so `DataRow` reports the error against the real column type.
            // Swallowing it here would turn a broken payload into a normal-looking 0.
            var ex = Record.Exception(() => JsonSerializer.Deserialize<DataTable>(json, s_options));
            Assert.IsType<ArgumentException>(ex);
        }

        public static TheoryData<FieldDbType> FieldDbTypes()
        {
            var data = new TheoryData<FieldDbType>();
            // `Unknown` names no column type; `AddColumn` rejects it.
            foreach (var dbType in Enum.GetValues<FieldDbType>().Where(t => t != FieldDbType.Unknown))
                data.Add(dbType);
            return data;
        }

        [Theory]
        [MemberData(nameof(FieldDbTypes))]
        [DisplayName("A column built by AddColumn reads back with its default value and a row that takes it")]
        public void AddColumn_EveryFieldDbType_RoundTripsWithDefault(FieldDbType dbType)
        {
            var table = new DataTable("T");
            var column = table.AddColumn("value", dbType);
            table.Rows.Add(table.NewRow());

            var restored = RoundTrip(table);

            // A Binary column's default is an empty `byte[]`. It was read back as the string "", which a
            // `byte[]` column rejects, so a table with a Binary column did not read back at all.
            var restoredColumn = restored.Columns["value"]!;
            Assert.Equal(column.DataType, restoredColumn.DataType);
            Assert.Equal(column.DefaultValue, restoredColumn.DefaultValue);
            Assert.Equal(table.Rows[0]["value"], restored.Rows[0]["value"]);
        }

        [Fact]
        [DisplayName("A default value written before the column's type is still read against that type")]
        public void DefaultValue_BeforeType_IsReadAgainstColumnType()
        {
            // The JSON object's member order is not guaranteed; a client in another language may write the
            // default first.
            const string json = """
                {
                  "tableName": "T",
                  "columns": [
                    { "defaultValue": "", "name": "photo", "type": "Binary", "allowNull": true },
                    { "defaultValue": "12345678901234.567", "name": "amount", "type": "Decimal", "allowNull": true },
                    { "defaultValue": "2026-07-28", "name": "note", "type": "String", "allowNull": true }
                  ],
                  "primaryKeys": [],
                  "rows": []
                }
                """;

            var restored = JsonSerializer.Deserialize<DataTable>(json, s_options)!;

            Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(restored.Columns["photo"]!.DefaultValue));
            Assert.Equal(12345678901234.567m, Assert.IsType<decimal>(restored.Columns["amount"]!.DefaultValue));
            // Read without the column's type, the text was taken for a date and came back as "07/28/2026 00:00:00".
            Assert.Equal("2026-07-28", restored.Columns["note"]!.DefaultValue);
        }
}
}
