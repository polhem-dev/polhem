using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Polhem.Core.Serialization;

namespace Polhem.Core.UnitTests.Serialization
{
    /// <summary>
    /// Tests for how <see cref="DataTableJsonConverter"/> tolerates JSON shapes its own writer never produces:
    /// null names and states, stray array elements, unknown column properties, and numbers beyond decimal.
    /// </summary>
    public class DataTableJsonConverterTolerantReadTests
    {
        private static JsonSerializerOptions Options()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new DataTableJsonConverter());
            return options;
        }

        [Fact]
        [DisplayName("A null table name reads as an empty table name")]
        public void Read_NullTableName_ReadsAsEmpty()
        {
            const string json = """{"tableName":null,"columns":[],"primaryKeys":[],"rows":[]}""";

            var table = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal(string.Empty, table.TableName);
        }

        [Fact]
        [DisplayName("Elements of the columns array that are not objects are skipped")]
        public void Read_ColumnsWithNonObjectElements_SkipsThem()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[1,"x",{"name":"Id","type":"Integer"},null],
                "primaryKeys":[],
                "rows":[]
            }
            """;

            var table = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            var column = Assert.Single(table.Columns.Cast<DataColumn>());
            Assert.Equal("Id", column.ColumnName);
            Assert.Equal(typeof(int), column.DataType);
        }

        [Fact]
        [DisplayName("An unknown array-valued property in a column definition is passed over")]
        public void Read_ColumnWithUnknownArrayProperty_KeepsKnownProperties()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Code","tags":[1,2],"type":"String","maxLength":8}],
                "primaryKeys":[],
                "rows":[]
            }
            """;

            var table = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal(8, table.Columns["Code"]!.MaxLength);
        }

        [Fact]
        [DisplayName("Elements of the rows array that are not objects are skipped")]
        public void Read_RowsWithNonObjectElements_SkipsThem()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Id","type":"Integer"}],
                "primaryKeys":[],
                "rows":[7,{"state":"Added","current":{"Id":3}},"x"]
            }
            """;

            var table = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            var row = Assert.Single(table.Rows.Cast<DataRow>());
            Assert.Equal(3, row["Id"]);
        }

        [Fact]
        [DisplayName("A null row state reads as an Added row")]
        public void Read_NullRowState_ReadsAsAdded()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Id","type":"Integer"}],
                "primaryKeys":[],
                "rows":[{"state":null,"current":{"Id":1}}]
            }
            """;

            var table = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            Assert.Equal(DataRowState.Added, table.Rows[0].RowState);
        }

        [Fact]
        [DisplayName("A number beyond the decimal range is read as a double")]
        public void Read_NumberBeyondDecimal_ReadsAsDouble()
        {
            const string json = """
            {
                "tableName":"T",
                "columns":[{"name":"Big","type":"String"}],
                "primaryKeys":[],
                "rows":[{"state":"Added","current":{"Big":1e300}}]
            }
            """;

            var table = JsonSerializer.Deserialize<DataTable>(json, Options())!;

            var text = Assert.IsType<string>(table.Rows[0]["Big"]);
            Assert.Equal(1e300, double.Parse(text, CultureInfo.InvariantCulture));
        }
    }
}
