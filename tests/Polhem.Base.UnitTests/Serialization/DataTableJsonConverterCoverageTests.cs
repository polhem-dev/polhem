using System.ComponentModel;
using System.Data;
using System.Text;
using System.Text.Json;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests.Serialization
{
    /// <summary>
    /// Coverage tests for DataTableJsonConverter: the null branches of calling Write/Read directly,
    /// a full round-trip over columns of each basic CLR type (Added/Modified/Deleted/Unchanged row states,
    /// null values, original values), and the edge branches of ReadColumnField / ReadValueMap / SetRowValues.
    /// </summary>
    public class DataTableJsonConverterCoverageTests
    {
        private static JsonSerializerOptions Options()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new DataTableJsonConverter());
            return opts;
        }

        private static readonly byte[] s_sampleBytes = { 0x01, 0x02, 0x03, 0xFF };
        private static readonly Guid s_sampleGuid = new("11112222-3333-4444-5555-666677778888");
        private static readonly DateTime s_sampleDate =
            new(2026, 4, 17, 8, 30, 0, DateTimeKind.Unspecified);

        // ---- Null branches of calling Write / Read directly (STJ never passes a top-level null to the converter) ----

        [Fact]
        [DisplayName("Write called directly with a null DataTable writes the null literal")]
        public void Write_NullValueDirectInvoke_WritesNull()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                new DataTableJsonConverter().Write(writer, null!, Options());
            }

            var result = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("null", result);
        }

        [Fact]
        [DisplayName("Read called directly on a null token returns null")]
        public void Read_NullTokenDirectInvoke_ReturnsNull()
        {
            var bytes = Encoding.UTF8.GetBytes("null");
            var reader = new Utf8JsonReader(bytes);
            reader.Read();

            var converter = new DataTableJsonConverter();
            var result = converter.Read(ref reader, typeof(DataTable), Options());

            Assert.Null(result);
        }

        // ---- Full round-trip over each basic column type and every row state ----

        private static DataTable BuildAllTypesTable()
        {
            var dt = new DataTable("Full");
            var strCol = new DataColumn("Str", typeof(string)) { AllowDBNull = true, Caption = "文字" };
            var flagCol = new DataColumn("Flag", typeof(bool)) { AllowDBNull = true };
            var shCol = new DataColumn("Sh", typeof(short)) { AllowDBNull = true };
            var intCol = new DataColumn("Int", typeof(int)) { AllowDBNull = false };
            var lngCol = new DataColumn("Lng", typeof(long)) { AllowDBNull = true };
            var decCol = new DataColumn("Dec", typeof(decimal)) { AllowDBNull = true };
            var dtCol = new DataColumn("Dt", typeof(DateTime)) { AllowDBNull = true };
            var gdCol = new DataColumn("Gd", typeof(Guid)) { AllowDBNull = true };
            var binCol = new DataColumn("Bin", typeof(byte[])) { AllowDBNull = true };

            dt.Columns.Add(strCol);
            dt.Columns.Add(flagCol);
            dt.Columns.Add(shCol);
            dt.Columns.Add(intCol);
            dt.Columns.Add(lngCol);
            dt.Columns.Add(decCol);
            dt.Columns.Add(dtCol);
            dt.Columns.Add(gdCol);
            dt.Columns.Add(binCol);
            dt.PrimaryKey = new[] { intCol };
            return dt;
        }

        private static void FillRow(DataRow row, string str, int id, bool flag)
        {
            row["Str"] = str;
            row["Flag"] = flag;
            row["Sh"] = (short)7;
            row["Int"] = id;
            row["Lng"] = 9_000_000_000L;
            row["Dec"] = 123.45m;
            row["Dt"] = s_sampleDate;
            row["Gd"] = s_sampleGuid;
            row["Bin"] = s_sampleBytes;
        }

        [Fact]
        [DisplayName("A full round-trip over each basic column type and every row state preserves types and values")]
        public void WriteRead_AllTypesAndRowStates_RoundTrips()
        {
            var dt = BuildAllTypesTable();

            var r0 = dt.NewRow(); FillRow(r0, "Keep", 1, true); dt.Rows.Add(r0);
            var r1 = dt.NewRow(); FillRow(r1, "Before", 2, false); dt.Rows.Add(r1);
            var r2 = dt.NewRow(); FillRow(r2, "Doomed", 3, true); dt.Rows.Add(r2);
            dt.AcceptChanges(); // r0/r1/r2 → Unchanged

            dt.Rows[1]["Str"] = "After"; // r1 → Modified
            dt.Rows[2].Delete();         // r2 → Deleted

            var r3 = dt.NewRow();
            FillRow(r3, "Fresh", 4, false);
            r3["Lng"] = DBNull.Value; // A null value exercises the null branch of `WriteRowValues`.
            dt.Rows.Add(r3);          // r3 → Added

            var json = JsonSerializer.Serialize(dt, Options());
            var restored = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal("Full", restored.TableName);
            Assert.Equal(9, restored.Columns.Count);
            Assert.Equal(4, restored.Rows.Count);
            Assert.Single(restored.PrimaryKey);
            Assert.Equal("Int", restored.PrimaryKey[0].ColumnName);

            // r0 Unchanged
            Assert.Equal(DataRowState.Unchanged, restored.Rows[0].RowState);
            Assert.Equal("Keep", restored.Rows[0]["Str"]);
            Assert.Equal((short)7, restored.Rows[0]["Sh"]);
            Assert.Equal(9_000_000_000L, restored.Rows[0]["Lng"]);
            Assert.Equal(123.45m, restored.Rows[0]["Dec"]);
            Assert.Equal(s_sampleDate, restored.Rows[0]["Dt"]);
            Assert.Equal(s_sampleGuid, restored.Rows[0]["Gd"]);
            Assert.Equal(s_sampleBytes, (byte[])restored.Rows[0]["Bin"]);
            Assert.True((bool)restored.Rows[0]["Flag"]);

            // r1 Modified: both current and original are restored.
            Assert.Equal(DataRowState.Modified, restored.Rows[1].RowState);
            Assert.Equal("After", restored.Rows[1]["Str", DataRowVersion.Current]);
            Assert.Equal("Before", restored.Rows[1]["Str", DataRowVersion.Original]);

            // r2 Deleted: only the original version.
            Assert.Equal(DataRowState.Deleted, restored.Rows[2].RowState);
            Assert.Equal("Doomed", restored.Rows[2]["Str", DataRowVersion.Original]);

            // r3 Added, with a null value.
            Assert.Equal(DataRowState.Added, restored.Rows[3].RowState);
            Assert.Equal("Fresh", restored.Rows[3]["Str"]);
            Assert.True(restored.Rows[3].IsNull("Lng"));
        }

        // ---- Null-coalescing fallback branches of ReadColumnField ----

        [Fact]
        [DisplayName("Read applies fallback defaults when a column's name, type and caption are null")]
        public void ReadColumns_NullNameTypeCaption_UsesFallbacks()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":null,"type":null,"allowNull":true,"readOnly":false,"maxLength":-1,"caption":null,"defaultValue":null}],
                "primaryKeys":[],
                "rows":[]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            // A null type falls back to "String", and the column is still created.
            Assert.Single(dt.Columns);
            Assert.Equal(typeof(string), dt.Columns[0].DataType);
        }

        // ---- Guard branch of ReadValueMap when the value is not StartObject (279/280) ----

        [Fact]
        [DisplayName("Read yields a row without values when current is not an object, without throwing")]
        public void ReadRows_CurrentNotObject_YieldsRowWithoutValues()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],
                "primaryKeys":[],
                "rows":[{"state":"Added","current":123}]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal(1, dt.Rows.Count);
            Assert.True(dt.Rows[0].IsNull("Id"));
        }

        // ---- Early return of SetRowValues when values is null (449) ----

        [Fact]
        [DisplayName("Read builds a row without values for an Added row with no current section (SetRowValues null branch)")]
        public void ReadRows_AddedRowWithoutCurrent_BuildsEmptyRow()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],
                "primaryKeys":[],
                "rows":[{"state":"Added"}]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal(1, dt.Rows.Count);
            Assert.Equal(DataRowState.Added, dt.Rows[0].RowState);
        }

        // ---- ReadPrimitiveValue: a date string takes the TryGetDateTime branch (309/313-314) ----

        [Fact]
        [DisplayName("Read parses a date string in a DateTime column through TryGetDateTime")]
        public void ReadRows_DateLikeStringInStringColumn_ParsedViaDateTimeBranch()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"When","type":"DateTime","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],
                "primaryKeys":[],
                "rows":[{"state":"Added","current":{"When":"2026-04-17T08:30:00"}}]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal(new DateTime(2026, 4, 17, 8, 30, 0), dt.Rows[0]["When"]);
        }
    }
}
