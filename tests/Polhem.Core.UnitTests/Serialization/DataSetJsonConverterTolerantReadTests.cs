using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Polhem.Core.Serialization;

namespace Polhem.Core.UnitTests.Serialization
{
    /// <summary>
    /// Tests for how <see cref="DataSetJsonConverter"/> tolerates JSON shapes its own writer never produces:
    /// stray elements in the tables array, and relation entries with null or unknown members.
    /// </summary>
    public class DataSetJsonConverterTolerantReadTests
    {
        private static JsonSerializerOptions Options()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new DataSetJsonConverter());
            options.Converters.Add(new DataTableJsonConverter());
            return options;
        }

        private const string MasterTable =
            """{"tableName":"M","columns":[{"name":"Id","type":"Integer"}],"primaryKeys":[],"rows":[]}""";

        private const string DetailTable =
            """{"tableName":"D","columns":[{"name":"MId","type":"Integer"}],"primaryKeys":[],"rows":[]}""";

        [Theory]
        [InlineData("""{"tables":[{{MasterTable}}]}""")]
        [InlineData("""{"dataSetName":null,"tables":[{{MasterTable}}]}""")]
        [InlineData("""{"dataSetName":"","tables":[{{MasterTable}}]}""")]
        [DisplayName("A payload without a data set name reads under the default name instead of throwing")]
        public void Read_MissingDataSetName_KeepsDefaultName(string template)
        {
            string json = template.Replace("{{MasterTable}}", MasterTable, StringComparison.Ordinal);

            var dataSet = JsonSerializer.Deserialize<DataSet>(json, Options())!;

            Assert.Equal(new DataSet().DataSetName, dataSet.DataSetName);
            Assert.Equal("M", Assert.Single(dataSet.Tables.Cast<DataTable>()).TableName);
        }

        [Fact]
        [DisplayName("Elements of the tables array that are not objects are skipped")]
        public void Read_TablesWithNonObjectElements_SkipsThem()
        {
            string json = $$"""{"dataSetName":"DS","tables":[1,{{MasterTable}},"x",null]}""";

            var dataSet = JsonSerializer.Deserialize<DataSet>(json, Options())!;

            var table = Assert.Single(dataSet.Tables.Cast<DataTable>());
            Assert.Equal("M", table.TableName);
        }

        [Fact]
        [DisplayName("A relation with a null name and an unknown array member is still built from its tables and columns")]
        public void Read_RelationWithNullNameAndUnknownMember_BuildsRelation()
        {
            string json = $$"""
            {
                "dataSetName":"DS",
                "tables":[{{MasterTable}},{{DetailTable}}],
                "relations":[{"name":null,"notes":["a","b"],"parentTable":"M","childTable":"D","parentColumns":["Id"],"childColumns":["MId"]}]
            }
            """;

            var dataSet = JsonSerializer.Deserialize<DataSet>(json, Options())!;

            var relation = Assert.Single(dataSet.Relations.Cast<DataRelation>());
            Assert.Equal("M", relation.ParentTable.TableName);
            Assert.Equal("D", relation.ChildTable.TableName);
        }

        [Fact]
        [DisplayName("A relation whose parent and child tables are null is skipped")]
        public void Read_RelationWithNullTables_IsSkipped()
        {
            string json = $$"""
            {
                "dataSetName":"DS",
                "tables":[{{MasterTable}},{{DetailTable}}],
                "relations":[{"name":"R","parentTable":null,"childTable":null,"parentColumns":["Id"],"childColumns":["MId"]}]
            }
            """;

            var dataSet = JsonSerializer.Deserialize<DataSet>(json, Options())!;

            Assert.Empty(dataSet.Relations);
            Assert.Equal(2, dataSet.Tables.Count);
        }
    }
}
