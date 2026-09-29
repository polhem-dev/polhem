using System.ComponentModel;
using System.Data;
using System.Text;
using System.Text.Json;
using Polhem.Core.Serialization;

namespace Polhem.Core.UnitTests.Serialization
{
    /// <summary>
    /// Edge and error path tests for DataSetJsonConverter:
    /// null output and input, a token other than StartObject, skipping unknown properties,
    /// tables or relations that are not arrays, and relations that reference a missing table or column.
    /// </summary>
    public class DataSetJsonConverterEdgeTests
    {
        private static JsonSerializerOptions Options()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new DataSetJsonConverter());
            opts.Converters.Add(new DataTableJsonConverter());
            return opts;
        }

        [Fact]
        [DisplayName("Write writes null for a null DataSet")]
        public void Write_NullValue_WritesNull()
        {
            var json = JsonSerializer.Serialize<DataSet?>(null, Options());
            Assert.Equal("null", json);
        }

        [Fact]
        [DisplayName("Read returns null for a null token")]
        public void Read_NullToken_ReturnsNull()
        {
            var restored = JsonSerializer.Deserialize<DataSet?>("null", Options());
            Assert.Null(restored);
        }

        [Fact]
        [DisplayName("Read throws JsonException for a token other than StartObject")]
        public void Read_NonStartObjectToken_Throws()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DataSet>("[1,2,3]", Options()));
        }

        [Fact]
        [DisplayName("Read skips unknown top-level properties")]
        public void Read_UnknownTopLevelProperty_IsIgnored()
        {
            const string json = """
            {
                "dataSetName":"Named",
                "unknown":{"a":1,"b":[1,2]},
                "tables":[],
                "relations":[]
            }
            """;
            var ds = JsonSerializer.Deserialize<DataSet>(json, Options())!;
            Assert.Equal("Named", ds.DataSetName);
            Assert.Empty(ds.Tables);
            Assert.Empty(ds.Relations);
        }

        [Fact]
        [DisplayName("Read yields no tables when tables is not an array")]
        public void Read_TablesNotArray_YieldsNoTables()
        {
            const string json = """
            {
                "dataSetName":"X",
                "tables":123,
                "relations":[]
            }
            """;
            var ds = JsonSerializer.Deserialize<DataSet>(json, Options())!;
            Assert.Empty(ds.Tables);
        }

        [Fact]
        [DisplayName("Read yields no relations when relations is not an array")]
        public void Read_RelationsNotArray_YieldsNoRelations()
        {
            const string json = """
            {
                "dataSetName":"X",
                "tables":[],
                "relations":"bad"
            }
            """;
            var ds = JsonSerializer.Deserialize<DataSet>(json, Options())!;
            Assert.Empty(ds.Relations);
        }

        [Fact]
        [DisplayName("Read skips a relation that references a missing table")]
        public void Read_RelationReferencesMissingTable_IsSkipped()
        {
            // `parentTable` names a table that does not exist, so `BuildDataSet` skips the relation.
            const string json = """
            {
                "dataSetName":"X",
                "tables":[
                    {"tableName":"Only","columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],"primaryKeys":[],"rows":[]}
                ],
                "relations":[
                    {"name":"Missing","parentTable":"Ghost","childTable":"Only","parentColumns":["Id"],"childColumns":["Id"]}
                ]
            }
            """;
            var ds = JsonSerializer.Deserialize<DataSet>(json, Options())!;
            Assert.Single(ds.Tables);
            Assert.Empty(ds.Relations);
        }

        [Fact]
        [DisplayName("Read skips a relation that references missing columns")]
        public void Read_RelationReferencesMissingColumns_IsSkipped()
        {
            // The column names match no column, so the filtered lists are empty and no relation is created.
            const string json = """
            {
                "dataSetName":"X",
                "tables":[
                    {"tableName":"A","columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],"primaryKeys":[],"rows":[]},
                    {"tableName":"B","columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],"primaryKeys":[],"rows":[]}
                ],
                "relations":[
                    {"name":"BadRel","parentTable":"A","childTable":"B","parentColumns":["NoSuch"],"childColumns":["NoSuch"]}
                ]
            }
            """;
            var ds = JsonSerializer.Deserialize<DataSet>(json, Options())!;
            Assert.Equal(2, ds.Tables.Count);
            Assert.Empty(ds.Relations);
        }

        [Fact]
        [DisplayName("Read skips relations elements that are not objects")]
        public void Read_RelationArrayContainsNonObjects_AreSkipped()
        {
            // The number element is skipped, and the valid relation after it is still read.
            const string json = """
            {
                "dataSetName":"X",
                "tables":[
                    {"tableName":"A","columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],"primaryKeys":[],"rows":[]},
                    {"tableName":"B","columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],"primaryKeys":[],"rows":[]}
                ],
                "relations":[
                    123,
                    {"name":"Rel","parentTable":"A","childTable":"B","parentColumns":["Id"],"childColumns":["Id"]}
                ]
            }
            """;
            var ds = JsonSerializer.Deserialize<DataSet>(json, Options())!;
            Assert.Single(ds.Relations);
            Assert.Equal("Rel", ds.Relations[0].RelationName);
        }

        [Fact]
        [DisplayName("Read ignores a parentColumns value that is not an array")]
        public void Read_RelationColumnsNotArray_YieldsEmptyColumnList()
        {
            // A string `parentColumns` makes `ReadStringArray` return an empty list, so the relation is not created.
            const string json = """
            {
                "dataSetName":"X",
                "tables":[
                    {"tableName":"A","columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],"primaryKeys":[],"rows":[]},
                    {"tableName":"B","columns":[{"name":"Id","type":"Integer","allowNull":true,"readOnly":false,"maxLength":-1,"caption":"","defaultValue":null}],"primaryKeys":[],"rows":[]}
                ],
                "relations":[
                    {"name":"Rel","parentTable":"A","childTable":"B","parentColumns":"not-array","childColumns":["Id"]}
                ]
            }
            """;
            var ds = JsonSerializer.Deserialize<DataSet>(json, Options())!;
            Assert.Empty(ds.Relations);
        }

        [Fact]
        [DisplayName("Write called directly on the converter with a null DataSet writes JSON null")]
        public void Write_DirectConverter_NullValue_WritesNull()
        {
            // `JsonSerializer.Serialize` short-circuits a null value and never calls `converter.Write`.
            // Only a direct call reaches the `value == null` branch.
            var converter = new DataSetJsonConverter();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                converter.Write(writer, null!, new JsonSerializerOptions());
            }

            var json = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("null", json);
        }

        [Fact]
        [DisplayName("Read called directly on the converter with a Null token returns null")]
        public void Read_DirectConverter_NullToken_ReturnsNull()
        {
            // `JsonSerializer.Deserialize` short-circuits a null token and never calls `converter.Read`.
            // Only a direct call reaches the `TokenType.Null` branch.
            var converter = new DataSetJsonConverter();
            var bytes = Encoding.UTF8.GetBytes("null");
            var reader = new Utf8JsonReader(bytes);
            Assert.True(reader.Read());

            var result = converter.Read(ref reader, typeof(DataSet), new JsonSerializerOptions());
            Assert.Null(result);
        }
    }
}
