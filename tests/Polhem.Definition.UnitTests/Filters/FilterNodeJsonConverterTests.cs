using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polhem.Base.Serialization;
using Polhem.Definition.Filters;

namespace Polhem.Definition.UnitTests.Filters
{
    /// <summary>
    /// Guards JSON polymorphism for members whose declared type is <see cref="FilterNode"/>.
    /// </summary>
    /// <remarks>
    /// System.Text.Json binds to the declared type: once a <see cref="FilterGroup"/> is assigned to a
    /// <see cref="FilterNode"/> property, without the converter only <c>{"kind":"Group"}</c> is written,
    /// and the operator and the whole subtree **disappear silently, without an exception**.
    /// <para>
    /// The hole existed for a long time without surfacing, because encoded bodies only ever went through MessagePack
    /// (which has its own filter node formatter). Once the JSON body codec shipped, it took effect on the most common list queries.
    /// </para>
    /// </remarks>
    public class FilterNodeJsonConverterTests
    {
        /// <summary>
        /// Carries a member whose declared type is <see cref="FilterNode"/>, which is exactly the shape that breaks.
        /// The annotation matches the actual holder on the wire (at the property level, not the type level).
        /// </summary>
        private sealed class FilterHolder
        {
            [JsonConverter(typeof(FilterNodeJsonConverter))]
            public FilterNode? Filter { get; set; }
        }

        private static FilterGroup BuildGroup()
        {
            var group = new FilterGroup(LogicalOperator.Or);
            group.Nodes.Add(new FilterCondition("amount", ComparisonOperator.GreaterThan, 100m));
            group.Nodes.Add(new FilterCondition("name", ComparisonOperator.Like, "A%"));
            return group;
        }

        [Fact]
        [DisplayName("A member declared as FilterNode writes the full subtree, not just the discriminator")]
        public void Serialize_FilterNodeMember_KeepsSubtree()
        {
            var json = JsonCodec.Serialize(new FilterHolder { Filter = BuildGroup() });

            Assert.Contains("\"nodes\"", json, StringComparison.Ordinal);
            Assert.Contains("amount", json, StringComparison.Ordinal);
            Assert.Contains("\"operator\":\"Or\"", json, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A member declared as FilterNode is restored to its original concrete type and content")]
        public void RoundTrip_FilterNodeMember_RestoresConcreteType()
        {
            var json = JsonCodec.Serialize(new FilterHolder { Filter = BuildGroup() });

            var restored = JsonCodec.Deserialize<FilterHolder>(json);

            var group = Assert.IsType<FilterGroup>(restored!.Filter);
            Assert.Equal(LogicalOperator.Or, group.Operator);
            Assert.Equal(2, group.Nodes.Count);
            var first = Assert.IsType<FilterCondition>(group.Nodes[0]);
            Assert.Equal("amount", first.FieldName);
            Assert.Equal(ComparisonOperator.GreaterThan, first.Operator);
        }

        [Fact]
        [DisplayName("A single condition assigned to a FilterNode member is restored as a FilterCondition")]
        public void RoundTrip_ConditionAsNode_RestoresCondition()
        {
            var holder = new FilterHolder
            {
                Filter = new FilterCondition("sys_id", ComparisonOperator.Equal, "E001")
            };

            var restored = JsonCodec.Deserialize<FilterHolder>(JsonCodec.Serialize(holder));

            var condition = Assert.IsType<FilterCondition>(restored!.Filter);
            Assert.Equal("sys_id", condition.FieldName);
            Assert.Equal(ComparisonOperator.Equal, condition.Operator);
        }

        [Fact]
        [DisplayName("A null FilterNode member is restored as null")]
        public void RoundTrip_NullNode_StaysNull()
        {
            var restored = JsonCodec.Deserialize<FilterHolder>(
                JsonCodec.Serialize(new FilterHolder { Filter = null }));

            Assert.Null(restored!.Filter);
        }
        // The request readers are case-insensitive for member names; the discriminator must follow them.
        private static readonly JsonSerializerOptions s_requestOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        [Theory]
        [InlineData("""{"Filter":{"Kind":"Group","Operator":"Or","Nodes":[{"FieldName":"a","Value":1}]}}""")]
        [InlineData("""{"filter":{"kind":"group","operator":"Or","nodes":[{"fieldName":"a","value":1}]}}""")]
        [InlineData("""{"filter":{"KIND":"GROUP","operator":"Or","nodes":[{"fieldName":"a","value":1}]}}""")]
        [DisplayName("A group whose discriminator differs in case is read as a group, not silently turned into an empty condition")]
        public void Read_DiscriminatorInAnotherCase_IsReadAsGroup(string json)
        {
            var holder = JsonSerializer.Deserialize<FilterHolder>(json, s_requestOptions)!;

            var group = Assert.IsType<FilterGroup>(holder.Filter);
            Assert.Equal(LogicalOperator.Or, group.Operator);
            Assert.Equal("a", Assert.IsType<FilterCondition>(Assert.Single(group.Nodes)).FieldName);
        }

        [Theory]
        [InlineData("""{"filter":{"kind":"Nope","fieldName":"a"}}""")]
        [InlineData("""{"filter":{"kind":7,"fieldName":"a"}}""")]
        [InlineData("""{"filter":{"kind":"1","fieldName":"a"}}""")]
        [InlineData("""{"filter":{"kind":null,"fieldName":"a"}}""")]
        [DisplayName("An unknown discriminator is rejected with a JsonException instead of defaulting to a condition")]
        public void Read_UnknownDiscriminator_Throws(string json)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FilterHolder>(json, s_requestOptions));
        }

        [Fact]
        [DisplayName("A PascalCase discriminator inside a node list is read case-insensitively as well")]
        public void Read_PascalCaseDiscriminatorInNodeList_IsReadAsGroup()
        {
            var json = """{"filter":{"kind":"Group","nodes":[{"Kind":"Group","Nodes":[{"fieldName":"a"}]}]}}""";

            var holder = JsonSerializer.Deserialize<FilterHolder>(json, s_requestOptions)!;

            var outer = Assert.IsType<FilterGroup>(holder.Filter);
            var inner = Assert.IsType<FilterGroup>(Assert.Single(outer.Nodes));
            Assert.Equal("a", Assert.IsType<FilterCondition>(Assert.Single(inner.Nodes)).FieldName);
        }

        [Fact]
        [DisplayName("A null element in a node list is rejected with a JsonException")]
        public void Read_NullElementInNodeList_Throws()
        {
            var json = """{"filter":{"kind":"Group","nodes":[null]}}""";

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FilterHolder>(json, s_requestOptions));
        }
    }
}
