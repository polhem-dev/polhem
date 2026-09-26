using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Polhem.Api.Core.Conversion;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Branch coverage of ApiInputConverter.Convert: a null source, a compatible type, JsonElement deserialization,
    /// returning the source as is for interface/abstract targets, property copying, and skipping mismatched types.
    /// </summary>
    public class ApiInputConverterTests
    {
        public class SourceDto
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public string Extra { get; set; } = string.Empty;
        }

        public class TargetDto
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public string ReadOnly { get; } = "init";
        }

        public interface IMarker { }

        public abstract class AbstractBase
        {
            public string Title { get; set; } = string.Empty;
        }

        public class ConcreteMarker : IMarker
        {
            public string Name { get; set; } = string.Empty;
        }

        public class TypeMismatchTarget
        {
            public int Name { get; set; }
        }

        [Fact]
        [DisplayName("Convert returns null for a null source")]
        public void Convert_NullSource_ReturnsNull()
        {
            var result = ApiInputConverter.Convert(null!, typeof(TargetDto));
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("Convert returns the source as is when it is already compatible with the target type")]
        public void Convert_SourceAssignableToTarget_ReturnsSame()
        {
            var source = new TargetDto { Name = "x", Age = 1 };
            var result = ApiInputConverter.Convert(source, typeof(TargetDto));
            Assert.Same(source, result);
        }

        [Fact]
        [DisplayName("Convert returns the original source when the target is an interface type")]
        public void Convert_TargetIsInterface_ReturnsSource()
        {
            // The source does not implement `IMarker`, so the `IsInstanceOfType` path is not taken; the interface target alone returns it.
            var source = new SourceDto { Name = "x" };
            var result = ApiInputConverter.Convert(source, typeof(IMarker));
            Assert.Same(source, result);
        }

        [Fact]
        [DisplayName("Convert returns the original source when the target is an abstract type")]
        public void Convert_TargetIsAbstract_ReturnsSource()
        {
            var source = new SourceDto { Name = "x" };
            var result = ApiInputConverter.Convert(source, typeof(AbstractBase));
            Assert.Same(source, result);
        }

        [Fact]
        [DisplayName("Convert copies public properties with the same name and a compatible type to a new target instance")]
        public void Convert_CopiesMatchingPropertiesToNewTarget()
        {
            var source = new SourceDto { Name = "Alice", Age = 30, Extra = "Z" };
            var result = ApiInputConverter.Convert(source, typeof(TargetDto));

            var target = Assert.IsType<TargetDto>(result);
            Assert.NotSame(source, target);
            Assert.Equal("Alice", target.Name);
            Assert.Equal(30, target.Age);
        }

        [Fact]
        [DisplayName("Convert skips properties whose target type is incompatible with the source")]
        public void Convert_SkipsPropertiesWithIncompatibleTypes()
        {
            var source = new SourceDto { Name = "Alice" };
            var result = ApiInputConverter.Convert(source, typeof(TypeMismatchTarget));

            var target = Assert.IsType<TypeMismatchTarget>(result);
            // `source.Name` is a string and `target.Name` is an int, so it is skipped and keeps the default 0.
            Assert.Equal(0, target.Name);
        }

        [Fact]
        [DisplayName("Convert deserializes a JsonElement source with camelCase names case-insensitively")]
        public void Convert_JsonElement_DeserializesCaseInsensitive()
        {
            var json = """{"name":"Bob","age":42}""";
            using var doc = JsonDocument.Parse(json);
            var element = doc.RootElement.Clone();

            var result = ApiInputConverter.Convert(element, typeof(TargetDto));

            var target = Assert.IsType<TargetDto>(result);
            Assert.Equal("Bob", target.Name);
            Assert.Equal(42, target.Age);
        }

        public class DataSetCarrier
        {
            public DataSet? DataSet { get; set; }
        }

        [Fact]
        [DisplayName("Convert restores RowState and column values from a JsonElement source through the DataTable converter")]
        public void Convert_JsonElement_DeserializesDataSetWithRowState()
        {
            // Regression: ApiInputConverter must include DataSet/DataTable/StringEnum
            // converters, otherwise Plain-format requests carrying a DataSet payload
            // silently deserialize to empty rows (Save sees "no pending changes").
            const string json = """
                {
                    "dataSet": {
                        "dataSetName": "Test",
                        "tables": [{
                            "tableName": "T",
                            "columns": [{
                                "name": "X",
                                "type": "String",
                                "allowNull": false,
                                "readOnly": false,
                                "maxLength": -1,
                                "caption": "X",
                                "defaultValue": ""
                            }],
                            "primaryKeys": [],
                            "rows": [{
                                "state": "Added",
                                "current": { "X": "v" }
                            }]
                        }],
                        "relations": []
                    }
                }
                """;
            using var doc = JsonDocument.Parse(json);
            var element = doc.RootElement.Clone();

            var result = ApiInputConverter.Convert(element, typeof(DataSetCarrier));

            var carrier = Assert.IsType<DataSetCarrier>(result);
            Assert.NotNull(carrier.DataSet);
            Assert.Single(carrier.DataSet.Tables);
            var table = carrier.DataSet.Tables[0];
            Assert.Equal("T", table.TableName);
            Assert.Single(table.Rows);
            Assert.Equal(DataRowState.Added, table.Rows[0].RowState);
            Assert.Equal("v", table.Rows[0]["X"]);
        }

        [Fact]
        [DisplayName("Convert accepts PascalCase property names in a JsonElement source")]
        public void Convert_JsonElement_AcceptsPascalCase()
        {
            var json = """{"Name":"Cathy","Age":7}""";
            using var doc = JsonDocument.Parse(json);
            var element = doc.RootElement.Clone();

            var result = ApiInputConverter.Convert(element, typeof(TargetDto));

            var target = Assert.IsType<TargetDto>(result);
            Assert.Equal("Cathy", target.Name);
            Assert.Equal(7, target.Age);
        }
    }
}
