using System.ComponentModel;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Tests for <see cref="DetailAggregateRewriter"/>: which calls are taken as detail aggregates and how they are
    /// replaced by variables.
    /// </summary>
    public class DetailAggregateRewriterTests
    {
        [Fact]
        [DisplayName("An expression without an aggregate is returned as written")]
        public void Rewrite_NoAggregate_ReturnsOriginal()
        {
            var result = DetailAggregateRewriter.Rewrite("quantity * unit_price");

            Assert.False(result.HasAggregates);
            Assert.Equal("quantity * unit_price", result.Expression);
        }

        [Fact]
        [DisplayName("Sum, Count, Min and Max calls become variables")]
        public void Rewrite_EachFunction_ReplacedByVariable()
        {
            var result = DetailAggregateRewriter.Rewrite(
                "Sum(Item.amount) + Count(Item) + Min(Item.amount) + Max( Item . amount )");

            Assert.Equal("__agg0 + __agg1 + __agg2 + __agg3", result.Expression);
            Assert.Collection(result.Aggregates,
                a => Assert.Equal((DetailAggregateFunction.Sum, "Item", "amount"), (a.Function, a.TableName, a.FieldName)),
                a => Assert.Equal((DetailAggregateFunction.Count, "Item", ""), (a.Function, a.TableName, a.FieldName)),
                a => Assert.Equal(DetailAggregateFunction.Min, a.Function),
                a => Assert.Equal((DetailAggregateFunction.Max, "Item", "amount"), (a.Function, a.TableName, a.FieldName)));
        }

        [Fact]
        [DisplayName("The same call written twice shares one variable")]
        public void Rewrite_RepeatedCall_SharesVariable()
        {
            var result = DetailAggregateRewriter.Rewrite("Sum(Item.amount) > 0 ? Sum(Item.amount) : 0m");

            Assert.Single(result.Aggregates);
            Assert.Equal("__agg0 > 0 ? __agg0 : 0m", result.Expression);
        }

        [Theory]
        [DisplayName("Calls of another shape are left as written")]
        [InlineData("Math.Max(a, b)")]
        [InlineData("Max(a, b)")]
        [InlineData("Count(Item.amount)")]
        [InlineData("Sum(Item)")]
        [InlineData("label == \"Sum(Item.amount)\"")]
        [InlineData("MySum(Item.amount)")]
        public void Rewrite_OtherShape_LeftAsWritten(string expression)
        {
            var result = DetailAggregateRewriter.Rewrite(expression);

            Assert.False(result.HasAggregates);
            Assert.Equal(expression, result.Expression);
        }
    }
}
