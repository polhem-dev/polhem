using System.ComponentModel;
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
    }
}
