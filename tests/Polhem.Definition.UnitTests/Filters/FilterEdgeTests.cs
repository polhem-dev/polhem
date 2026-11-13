using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Polhem.Definition.Filters;

namespace Polhem.Definition.UnitTests.Filters
{
    /// <summary>
    /// Edge cases of the filter model: direct calls on <see cref="FilterNodeJsonConverter"/>, the text form of
    /// <see cref="FilterCondition"/> for unusual values, and the group factories given no nodes.
    /// </summary>
    public class FilterEdgeTests
    {
        [Fact]
        [DisplayName("Write called directly on the node converter writes JSON null for a null node")]
        public void Write_DirectConverter_NullNode_WritesNull()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                new FilterNodeJsonConverter().Write(writer, null!, new JsonSerializerOptions());
            }

            Assert.Equal("null", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        [DisplayName("Read called directly on the node converter returns null for a Null token")]
        public void Read_DirectConverter_NullToken_ReturnsNull()
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("null"));
            Assert.True(reader.Read());

            Assert.Null(new FilterNodeJsonConverter().Read(ref reader, typeof(FilterNode), new JsonSerializerOptions()));
        }

        [Fact]
        [DisplayName("Reading a filter node from a token that is not an object throws JsonException")]
        public void Read_ArrayToken_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => ReadDirect("[1]"));
        }

        [Fact]
        [DisplayName("A node list element that is not an object is rejected with a JsonException")]
        public void Read_NodeListWithNumberElement_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => ReadDirect("{\"Kind\":\"Group\",\"Nodes\":[42]}"));
        }

        [Fact]
        [DisplayName("A discriminator spelled as a number string is rejected rather than parsed as the enum value")]
        public void Read_NumericStringKind_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => ReadDirect("{\"kind\":\"1\"}"));
        }

        [Fact]
        [DisplayName("A discriminator that is neither a string nor a number is rejected")]
        public void Read_BooleanKind_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => ReadDirect("{\"kind\":true}"));
        }

        [Fact]
        [DisplayName("ToString of an operator outside the known set falls back to the operator's numeric name")]
        public void FilterCondition_ToString_UnknownOperator_UsesEnumName()
        {
            var condition = new FilterCondition("F", (ComparisonOperator)99, 5);

            Assert.Equal("F 99 5", condition.ToString());
        }

        [Fact]
        [DisplayName("ToString of IN over a single value rather than a list prints the value itself")]
        public void FilterCondition_ToString_InWithScalarValue_PrintsValue()
        {
            var condition = new FilterCondition("F", ComparisonOperator.In, "x");

            Assert.Equal("F IN 'x'", condition.ToString());
        }

        [Fact]
        [DisplayName("ToString of BETWEEN quotes string bounds")]
        public void FilterCondition_ToString_BetweenStrings_QuotesBounds()
        {
            var condition = FilterCondition.Between("code", "A", "M");

            Assert.Equal("code BETWEEN 'A' AND 'M'", condition.ToString());
        }

        [Fact]
        [DisplayName("ToString of a null value prints nothing after the operator")]
        public void FilterCondition_ToString_NullValue_PrintsEmpty()
        {
            var condition = new FilterCondition("F", ComparisonOperator.Equal, null!);

            Assert.Equal("F = ", condition.ToString());
        }

        [Fact]
        [DisplayName("FilterGroup.All and FilterGroup.Any given a null array create an empty group")]
        public void FilterGroupFactories_NullNodes_CreateEmptyGroups()
        {
            var all = FilterGroup.All(null!);
            var any = FilterGroup.Any(null!);

            Assert.Equal(LogicalOperator.And, all.Operator);
            Assert.Empty(all.Nodes);
            Assert.Equal(LogicalOperator.Or, any.Operator);
            Assert.Empty(any.Nodes);
        }

        private static FilterNode? ReadDirect(string json)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
            reader.Read();
            return new FilterNodeJsonConverter().Read(ref reader, typeof(FilterNode), new JsonSerializerOptions());
        }
    }
}
