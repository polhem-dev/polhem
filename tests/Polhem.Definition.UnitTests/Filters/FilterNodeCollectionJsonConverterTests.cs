using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Polhem.Definition.Filters;

namespace Polhem.Definition.UnitTests.Filters
{
    /// <summary>
    /// Read/Write tests for FilterNodeCollectionJsonConverter.
    /// </summary>
    public class FilterNodeCollectionJsonConverterTests
    {
        // In practice the outer payload envelope is serialized with the camelCase naming policy, which lets the converter's Read path match the lowercase "kind" property.
        private static readonly JsonSerializerOptions s_options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new FilterNodeCollectionJsonConverter() }
        };

        [Fact]
        [DisplayName("Write outputs null for a null collection")]
        public void Write_NullCollection_WritesNull()
        {
            FilterNodeCollection? collection = null;
            var json = JsonSerializer.Serialize(collection, s_options);
            Assert.Equal("null", json);
        }

        [Fact]
        [DisplayName("Write outputs [] for an empty collection")]
        public void Write_EmptyCollection_WritesEmptyArray()
        {
            var collection = new FilterNodeCollection();
            var json = JsonSerializer.Serialize(collection, s_options);
            Assert.Equal("[]", json);
        }

        [Fact]
        [DisplayName("Write/Read round-trip restores a FilterCondition")]
        public void ReadWrite_RoundTrip_FilterCondition()
        {
            var collection = new FilterNodeCollection
            {
                new FilterCondition("Name", ComparisonOperator.Equal, "Alice")
            };

            var json = JsonSerializer.Serialize(collection, s_options);
            var restored = JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options);

            Assert.NotNull(restored);
            Assert.Single(restored!);
            var cond = Assert.IsType<FilterCondition>(restored[0]);
            Assert.Equal("Name", cond.FieldName);
            Assert.Equal(ComparisonOperator.Equal, cond.Operator);
        }

        [Fact]
        [DisplayName("Write/Read round-trip restores a FilterGroup with its child nodes")]
        public void ReadWrite_RoundTrip_FilterGroup()
        {
            var group = new FilterGroup(LogicalOperator.Or);
            group.Nodes.Add(new FilterCondition("Age", ComparisonOperator.GreaterThan, 18));

            var collection = new FilterNodeCollection
            {
                group
            };

            var json = JsonSerializer.Serialize(collection, s_options);
            var restored = JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options);

            Assert.NotNull(restored);
            Assert.Single(restored!);
            var g = Assert.IsType<FilterGroup>(restored[0]);
            Assert.Equal(LogicalOperator.Or, g.Operator);
            Assert.Single(g.Nodes);
        }

        [Fact]
        [DisplayName("Read returns null for a null token")]
        public void Read_NullToken_ReturnsNull()
        {
            var result = JsonSerializer.Deserialize<FilterNodeCollection>("null", s_options);
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("Read throws JsonException for a token that is not StartArray")]
        public void Read_NonStartArray_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<FilterNodeCollection>("123", s_options));
        }

        [Fact]
        [DisplayName("Read resolves kind as the string 'Condition' to a FilterCondition")]
        public void Read_StringKindCondition_ParsesAsFilterCondition()
        {
            var json = """[{"kind":"Condition","fieldName":"X","operator":0,"value":"a"}]""";
            var restored = JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options);

            Assert.NotNull(restored);
            Assert.Single(restored!);
            Assert.IsType<FilterCondition>(restored[0]);
        }

        [Fact]
        [DisplayName("Read resolves kind as the string 'Group' to a FilterGroup")]
        public void Read_StringKindGroup_ParsesAsFilterGroup()
        {
            var json = """[{"kind":"Group","operator":0,"nodes":[]}]""";
            var restored = JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options);

            Assert.NotNull(restored);
            Assert.Single(restored!);
            Assert.IsType<FilterGroup>(restored[0]);
        }

        [Fact]
        [DisplayName("Read resolves kind as the integer 0 to a FilterCondition")]
        public void Read_IntKindCondition_ParsesAsFilterCondition()
        {
            var json = """[{"kind":0,"fieldName":"X","operator":0,"value":"a"}]""";
            var restored = JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options);

            Assert.NotNull(restored);
            Assert.IsType<FilterCondition>(restored![0]);
        }

        [Fact]
        [DisplayName("Read resolves kind as the integer 1 to a FilterGroup")]
        public void Read_IntKindGroup_ParsesAsFilterGroup()
        {
            var json = """[{"kind":1,"operator":0,"nodes":[]}]""";
            var restored = JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options);

            Assert.NotNull(restored);
            Assert.IsType<FilterGroup>(restored![0]);
        }

        [Fact]
        [DisplayName("Read defaults an element without a kind property to a FilterCondition")]
        public void Read_ElementWithoutKind_DefaultsToFilterCondition()
        {
            var json = """[{"fieldName":"X","operator":0,"value":"a"}]""";
            var restored = JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options);

            Assert.NotNull(restored);
            Assert.Single(restored!);
            Assert.IsType<FilterCondition>(restored[0]);
        }

        [Fact]
        [DisplayName("Read throws JsonException for an unknown integer kind")]
        public void Read_UnknownIntKind_ThrowsJsonException()
        {
            var json = """[{"kind":99}]""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<FilterNodeCollection>(json, s_options));
        }

        [Fact]
        [DisplayName("Write called directly on the converter writes JSON null for a null collection")]
        public void Write_DirectConverter_NullCollection_WritesNull()
        {
            // `JsonSerializer.Serialize<FilterNodeCollection?>(null, ...)` is short-circuited by the framework,
            // which writes null without entering `converter.Write`. Only a direct call covers the `value == null` branch.
            var converter = new FilterNodeCollectionJsonConverter();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                converter.Write(writer, null!, new JsonSerializerOptions());
            }

            var json = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("null", json);
        }

        [Fact]
        [DisplayName("Read called directly on the converter returns null for a Null token")]
        public void Read_DirectConverter_NullToken_ReturnsNull()
        {
            // `JsonSerializer.Deserialize<FilterNodeCollection?>("null", ...)` is short-circuited by the framework,
            // which returns null without entering `converter.Read`. Only a direct call covers the `TokenType.Null` branch.
            var converter = new FilterNodeCollectionJsonConverter();
            var bytes = Encoding.UTF8.GetBytes("null");
            var reader = new Utf8JsonReader(bytes);
            Assert.True(reader.Read());

            var result = converter.Read(ref reader, typeof(FilterNodeCollection), new JsonSerializerOptions());
            Assert.Null(result);
        }
    }
}
