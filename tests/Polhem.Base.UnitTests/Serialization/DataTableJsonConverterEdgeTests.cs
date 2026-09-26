using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Polhem.Base.Serialization;

namespace Polhem.Base.UnitTests.Serialization
{
    /// <summary>
    /// Edge and error path tests for DataTableJsonConverter:
    /// unexpected JSON tokens, primitive and non-primitive values,
    /// the type conversion branches of ConvertValue, and null handling in Read and Write.
    /// </summary>
    public class DataTableJsonConverterEdgeTests
    {
        private static JsonSerializerOptions Options()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new DataTableJsonConverter());
            return opts;
        }

        [Fact]
        [DisplayName("Read throws JsonException for a token other than StartObject")]
        public void Read_NonStartObjectToken_Throws()
        {
            const string json = "[1,2,3]";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DataTable>(json, Options()));
        }

        [Fact]
        [DisplayName("Read returns null for a null token")]
        public void Read_NullToken_ReturnsNull()
        {
            var restored = JsonSerializer.Deserialize<DataTable?>("null", Options());
            Assert.Null(restored);
        }

        [Fact]
        [DisplayName("Write writes null for a null DataTable")]
        public void Write_NullValue_WritesNull()
        {
            var json = JsonSerializer.Serialize<DataTable?>(null, Options());
            Assert.Equal("null", json);
        }

        [Fact]
        [DisplayName("Read skips unknown top-level properties")]
        public void Read_UnknownTopLevelProperty_IsIgnored()
        {
            // The unknown property is skipped by `reader.Skip()` in the default case.
            const string json = """
            {
                "tableName":"T",
                "unknown":{"nested":"value"},
                "columns":[],
                "primaryKeys":[],
                "rows":[]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Equal("T", dt.TableName);
        }

        [Fact]
        [DisplayName("Read yields no columns when columns is not an array")]
        public void Read_ColumnsNotArray_YieldsNoColumns()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":123,
                "primaryKeys":[],
                "rows":[]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Empty(dt.Columns);
        }

        [Fact]
        [DisplayName("Read does not throw and sets no primary key when primaryKeys is not an array")]
        public void Read_PrimaryKeysNotArray_IsSafe()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"Id","defaultValue":null}],
                "primaryKeys":"bad",
                "rows":[]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Empty(dt.PrimaryKey);
        }

        [Fact]
        [DisplayName("Read yields no rows when rows is not an array")]
        public void Read_RowsNotArray_YieldsNoRows()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"Id","defaultValue":null}],
                "primaryKeys":[],
                "rows":null
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Empty(dt.Rows);
        }

        [Fact]
        [DisplayName("Read skips unknown properties in a row")]
        public void Read_UnknownRowProperty_IsSkipped()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"Id","defaultValue":null}],
                "primaryKeys":[],
                "rows":[{"state":"Added","current":{"Id":1},"extra":{"nested":1}}]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Equal(1, dt.Rows.Count);
            Assert.Equal(1, dt.Rows[0]["Id"]);
        }

        [Fact]
        [DisplayName("Read sets the column default value when defaultValue is a primitive")]
        public void Read_ColumnWithPrimitiveDefault_UsesDefault()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Name","type":"String","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":"hello"}],
                "primaryKeys":[],
                "rows":[]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Equal("hello", dt.Columns["Name"]!.DefaultValue);
        }

        [Fact]
        [DisplayName("Read returns DBNull for a null current value in a numeric column")]
        public void Read_NullValueInCurrent_IsDbNull()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Age","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],
                "primaryKeys":[],
                "rows":[{"state":"Added","current":{"Age":null}}]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.True(dt.Rows[0].IsNull("Age"));
        }

        [Fact]
        [DisplayName("ConvertValue returns the original bytes for a byte[] target and a Base64 string")]
        public void ConvertValue_ByteArrayFromBase64_ReturnsBytes()
        {
            var bytes = new byte[] { 0x01, 0x02, 0xAB, 0xFF };
            var base64 = Convert.ToBase64String(bytes);

            var result = DataTableJsonConverter.ConvertValue(base64, typeof(byte[]));

            var typed = Assert.IsType<byte[]>(result);
            Assert.Equal(bytes, typed);
        }

        [Fact]
        [DisplayName("ConvertValue returns a non-string input unchanged for a byte[] target")]
        public void ConvertValue_ByteArrayNonString_ReturnsSame()
        {
            var input = 123L;
            var result = DataTableJsonConverter.ConvertValue(input, typeof(byte[]));
            Assert.Equal(input, result);
        }

        [Fact]
        [DisplayName("ConvertValue parses a string into a Guid for a Guid target")]
        public void ConvertValue_GuidFromString_ReturnsGuid()
        {
            var guid = Guid.NewGuid();
            var result = DataTableJsonConverter.ConvertValue(guid.ToString(), typeof(Guid));
            Assert.Equal(guid, result);
        }

        [Fact]
        [DisplayName("ConvertValue returns a non-string input unchanged for a Guid target")]
        public void ConvertValue_GuidNonString_ReturnsSame()
        {
            var result = DataTableJsonConverter.ConvertValue(42L, typeof(Guid));
            Assert.Equal(42L, result);
        }

        [Fact]
        [DisplayName("ConvertValue returns a DateTime value as is for a DateTime target")]
        public void ConvertValue_DateTimeFromDateTime_ReturnsSame()
        {
            var dt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var result = DataTableJsonConverter.ConvertValue(dt, typeof(DateTime));
            Assert.Equal(dt, result);
        }

        [Fact]
        [DisplayName("ConvertValue parses a string for a DateTime target")]
        public void ConvertValue_DateTimeFromString_ReturnsParsed()
        {
            var result = DataTableJsonConverter.ConvertValue("2026-04-17T08:30:00", typeof(DateTime));
            var typed = Assert.IsType<DateTime>(result);
            Assert.Equal(new DateTime(2026, 4, 17, 8, 30, 0), typed);
        }

        [Fact]
        [DisplayName("ConvertValue returns a long input unchanged for a DateTime target because it cannot convert it")]
        public void ConvertValue_DateTimeFromLong_ReturnsSameAsFallback()
        {
            // A long cannot become a `DateTime`. It enters the `DateTime` branch, matches neither `if`, and is
            // returned unchanged.
            var result = DataTableJsonConverter.ConvertValue(12345L, typeof(DateTime));
            Assert.Equal(12345L, result);
        }

        [Fact]
        [DisplayName("ConvertValue converts numeric types with Convert.ChangeType")]
        public void ConvertValue_LongToInt_Converts()
        {
            var result = DataTableJsonConverter.ConvertValue(42L, typeof(int));
            Assert.Equal(42, result);
            Assert.IsType<int>(result);
        }

        [Fact]
        [DisplayName("ConvertValue returns the input unchanged through the catch for an incompatible combination")]
        public void ConvertValue_IncompatibleType_ReturnsSameOnCatch()
        {
            // Converting a plain object to int makes `Convert.ChangeType` throw, and the catch returns the input.
            var input = new object();
            var result = DataTableJsonConverter.ConvertValue(input, typeof(int));
            Assert.Same(input, result);
        }

        [Fact]
        [DisplayName("ReadPrimitiveValue reads true and false tokens and turns a complex token into DBNull")]
        public void Read_BooleanAndComplexTokens_HandledCorrectly()
        {
            // Column C is a String column on purpose, so the type lookup takes the `ConvertValue` branch.
            const string json = """
            {
                "tableName":"T",
                "columns":[
                    {"name":"A","type":"Boolean","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null},
                    {"name":"B","type":"Boolean","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null},
                    {"name":"C","type":"String","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}
                ],
                "primaryKeys":[],
                "rows":[{"state":"Added","current":{"A":true,"B":false,"C":{"complex":"object"}}}]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.True((bool)dt.Rows[0]["A"]);
            Assert.False((bool)dt.Rows[0]["B"]);
            // A complex token is skipped and read as null, which becomes DBNull.
            Assert.True(dt.Rows[0].IsNull("C"));
        }

        [Fact]
        [DisplayName("Read falls back to double for a number beyond the long range")]
        public void Read_NumberBeyondLong_FallsBackToDouble()
        {
            // A number in a String column: `TryGetInt64` fails, so `GetDouble` is used.
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Big","type":"String","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],
                "primaryKeys":[],
                "rows":[{"state":"Added","current":{"Big":1.5e20}}]
            }
            """;
            var dt = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            // The value is converted to a string through `Convert.ChangeType`.
            Assert.NotNull(dt.Rows[0]["Big"]);
        }

        private static DataTable BuildSampleTable()
        {
            var dt = new DataTable("T");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            return dt;
        }

        [Fact]
        [DisplayName("Write outputs the current and original sections of a Modified row, and the row round-trips")]
        public void WriteRead_ModifiedRow_RoundTrip()
        {
            var dt = BuildSampleTable();
            dt.Rows.Add(1, "Alice");
            dt.AcceptChanges();
            dt.Rows[0]["Name"] = "Bob";
            Assert.Equal(DataRowState.Modified, dt.Rows[0].RowState);

            var json = JsonSerializer.Serialize(dt, Options());
            Assert.Contains("\"state\":\"Modified\"", json);
            Assert.Contains("\"current\":", json);
            Assert.Contains("\"original\":", json);

            var restored = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Equal(1, restored.Rows.Count);
            Assert.Equal(DataRowState.Modified, restored.Rows[0].RowState);
            Assert.Equal("Bob", restored.Rows[0]["Name", DataRowVersion.Current]);
            Assert.Equal("Alice", restored.Rows[0]["Name", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("Write outputs only the original section of a Deleted row, and the row round-trips")]
        public void WriteRead_DeletedRow_RoundTrip()
        {
            var dt = BuildSampleTable();
            dt.Rows.Add(1, "Alice");
            dt.AcceptChanges();
            dt.Rows[0].Delete();
            Assert.Equal(DataRowState.Deleted, dt.Rows[0].RowState);

            var json = JsonSerializer.Serialize(dt, Options());
            Assert.Contains("\"state\":\"Deleted\"", json);
            Assert.Contains("\"original\":", json);
            Assert.DoesNotContain("\"current\":", json);

            var restored = JsonSerializer.Deserialize<DataTable>(json, Options())!;
            Assert.Equal(1, restored.Rows.Count);
            Assert.Equal(DataRowState.Deleted, restored.Rows[0].RowState);
            Assert.Equal("Alice", restored.Rows[0]["Name", DataRowVersion.Original]);
        }

        [Fact]
        [DisplayName("Write skips Detached rows")]
        public void Write_DetachedRow_IsSkipped()
        {
            var dt = BuildSampleTable();
            dt.Rows.Add(1, "Alice");
            dt.AcceptChanges();
            var detached = dt.NewRow();
            detached["Id"] = 2;
            detached["Name"] = "Ghost";
            Assert.Equal(DataRowState.Detached, detached.RowState);

            // A detached row is not in `dt.Rows`, so `Write` never sees it and the JSON contains only Alice.
            var json = JsonSerializer.Serialize(dt, Options());
            Assert.Contains("Alice", json);
            Assert.DoesNotContain("Ghost", json);
        }

        [Fact]
        [DisplayName("Write outputs only current for an Unchanged row, without repeating original")]
        public void Write_UnchangedRow_WritesCurrentOnly()
        {
            // This test used to assert that both versions are written. That was a defect written down as a spec:
            // the two versions of an Unchanged row are equal by definition, and the Unchanged branch on the reading
            // side reads only current and calls `AcceptChanges` (covered by `ReadWrite_UnchangedRow_RoundTrip`).
            // Nothing read the extra copy. `DataFormRepository.GetData` calls `AcceptChanges()` before returning,
            // so every row read from the database takes this path, and the payload and serialization cost were doubled.
            var dt = BuildSampleTable();
            dt.Rows.Add(1, "Alice");
            dt.AcceptChanges();
            Assert.Equal(DataRowState.Unchanged, dt.Rows[0].RowState);

            var json = JsonSerializer.Serialize(dt, Options());
            Assert.Contains("\"state\":\"Unchanged\"", json);
            Assert.Contains("\"current\":", json);
            Assert.DoesNotContain("\"original\":", json);
        }

        [Fact]
        [DisplayName("Write serializes the full schema, including the primary key and column default values")]
        public void Write_ColumnsAndPrimaryKey_AreSerialized()
        {
            var dt = new DataTable("T");
            var idCol = new DataColumn("Id", typeof(int))
            {
                AllowDBNull = false,
                DefaultValue = 0
            };
            dt.Columns.Add(idCol);
            dt.Columns.Add(new DataColumn("Name", typeof(string)) { MaxLength = 32, Caption = "DisplayName" });
            dt.PrimaryKey = new[] { idCol };

            var json = JsonSerializer.Serialize(dt, Options());

            Assert.Contains("\"tableName\":\"T\"", json);
            Assert.Contains("\"primaryKeys\":[\"Id\"]", json);
            Assert.Contains("\"caption\":\"DisplayName\"", json);
            Assert.Contains("\"maxLength\":32", json);
            // The default value of the `Id` column is 0, not null, so the non-null branch is taken.
            Assert.Contains("\"defaultValue\":0", json);
        }

        [Fact]
        [DisplayName("Read restores an Unchanged row and calls AcceptChanges")]
        public void ReadWrite_UnchangedRow_RoundTrip()
        {
            // `Write_UnchangedRow_WritesCurrentOnly` checks only the write side. This test runs a full round-trip so
            // the restore logic enters the `DataRowState.Unchanged` case (line 418-422) and calls `AcceptChanges`.
            var dt = BuildSampleTable();
            dt.Rows.Add(1, "Alice");
            dt.AcceptChanges();
            Assert.Equal(DataRowState.Unchanged, dt.Rows[0].RowState);

            var json = JsonSerializer.Serialize(dt, Options());
            var restored = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal(1, restored.Rows.Count);
            Assert.Equal(DataRowState.Unchanged, restored.Rows[0].RowState);
            Assert.Equal(1, restored.Rows[0]["Id"]);
            Assert.Equal("Alice", restored.Rows[0]["Name"]);
        }
    }
}
