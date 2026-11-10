using System.ComponentModel;
using Polhem.DefineEditor.Services;
using Polhem.Expressions;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// What the expression editor's completion offers at the caret: names where a name starts, members after a dot,
    /// and a drop-down field's values inside the quotes it is compared with.
    /// </summary>
    public sealed class ExpressionCompletionTests
    {
        private static readonly ExpressionCompletionSources s_sources = new(
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["amount"] = typeof(decimal),
                ["note"] = typeof(string),
                ["order_date"] = typeof(DateOnly),
                ["status"] = typeof(string),
            },
            DynamicExpressoEvaluator.HelperFunctions,
            new DynamicExpressoEvaluator().ReferencedTypes,
            name => name == "status" ? ["open", "closed"] : null);

        // The caret is where `|` is; the bar is removed from the text.
        private static ExpressionSuggestions At(string textWithCaret)
        {
            var offset = textWithCaret.IndexOf('|', StringComparison.Ordinal);
            return ExpressionCompletion.At(textWithCaret.Remove(offset, 1), offset, s_sources);
        }

        private static List<string> Labels(ExpressionSuggestions suggestions, ExpressionSuggestionKind kind) =>
            suggestions.Items.Where(i => i.Kind == kind).Select(i => i.Label).ToList();

        [Fact]
        [DisplayName("Where a name starts, the fields, helper functions, featured types and keywords are offered")]
        public void At_NameStart_OffersNames()
        {
            var suggestions = At("amount > 0 && n|");

            Assert.Equal("amount > 0 && ".Length, suggestions.ReplaceFrom);
            Assert.Contains("note", Labels(suggestions, ExpressionSuggestionKind.Field));
            Assert.Contains("Today", Labels(suggestions, ExpressionSuggestionKind.Function));
            Assert.Contains("Math", Labels(suggestions, ExpressionSuggestionKind.Type));
            Assert.Contains("string", Labels(suggestions, ExpressionSuggestionKind.Type));
            Assert.DoesNotContain("String", Labels(suggestions, ExpressionSuggestionKind.Type));
            Assert.DoesNotContain("Int32", Labels(suggestions, ExpressionSuggestionKind.Type));
            Assert.Contains("true", Labels(suggestions, ExpressionSuggestionKind.Keyword));
        }

        [Fact]
        [DisplayName("A function without parameters is inserted with both parentheses, one with parameters with the opening one")]
        public void At_Functions_InsertParentheses()
        {
            var items = At("|").Items;

            Assert.Equal("Today()", items.Single(i => i.Label == "Today").InsertText);
            Assert.Equal("IsNullOrEmpty(", items.Single(i => i.Label == "IsNullOrEmpty").InsertText);
        }

        [Fact]
        [DisplayName("After a string field and a dot, the members of a string are offered, not those of object")]
        public void At_DotAfterStringField_OffersStringMembers()
        {
            var suggestions = At("note.|");

            var members = Labels(suggestions, ExpressionSuggestionKind.Member);
            Assert.Equal("note.".Length, suggestions.ReplaceFrom);
            Assert.Contains("Contains", members);
            Assert.Contains("Length", members);
            Assert.DoesNotContain("GetType", members);
            Assert.DoesNotContain("GetHashCode", members);
        }

        [Fact]
        [DisplayName("After a type name and a dot, its static members are offered")]
        public void At_DotAfterType_OffersStaticMembers()
        {
            var members = Labels(At("Math.R|"), ExpressionSuggestionKind.Member);

            Assert.Contains("Round", members);
            Assert.Contains("PI", members);
        }

        [Fact]
        [DisplayName("A member with overloads is described by its decimal overload, with the number of the others")]
        public void At_OverloadedMember_DescribesDecimalOverload()
        {
            var abs = At("Math.|").Items.Single(i => i.Label == "Abs");

            Assert.StartsWith("(decimal value) → decimal", abs.Detail, StringComparison.Ordinal);
            Assert.Contains("+", abs.Detail, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("DateTime's own Now, Today and UtcNow are not offered, in favour of the zone-aware helper functions")]
        public void At_DotAfterDateTime_LeavesOutServerClock()
        {
            var members = Labels(At("DateTime.|"), ExpressionSuggestionKind.Member);

            Assert.DoesNotContain("Now", members);
            Assert.DoesNotContain("Today", members);
            Assert.DoesNotContain("UtcNow", members);
            Assert.Contains("DaysInMonth", members);
        }

        [Fact]
        [DisplayName("A chain of calls is followed: Today() is a date, and its AddDays is a date too")]
        public void At_DotAfterCallChain_ResolvesReturnTypes()
        {
            Assert.Contains("AddDays", Labels(At("Today().|"), ExpressionSuggestionKind.Member));
            Assert.Contains("Year", Labels(At("Today().AddDays(7).|"), ExpressionSuggestionKind.Member));
            Assert.Contains("ToUpper", Labels(At("note.Trim().|"), ExpressionSuggestionKind.Member));
        }

        [Fact]
        [DisplayName("A dot in a number or after something unknown offers nothing")]
        public void At_DotAfterNumberOrUnknown_OffersNothing()
        {
            Assert.Empty(At("amount * 1.|").Items);
            Assert.Empty(At("missing.|").Items);
        }

        [Fact]
        [DisplayName("Inside quotes compared with a drop-down field, its values are offered with the closing quote")]
        public void At_QuoteAfterListField_OffersValues()
        {
            var suggestions = At("status == \"|");

            Assert.Equal(["open", "closed"], Labels(suggestions, ExpressionSuggestionKind.Value));
            Assert.Equal("open\"", suggestions.Items[0].InsertText);
            Assert.Equal("status == \"".Length, suggestions.ReplaceFrom);
        }

        [Fact]
        [DisplayName("When the closing quote is already there, a value is inserted without another")]
        public void At_QuoteAlreadyClosed_InsertsValueOnly()
        {
            Assert.Equal("open", At("status != \"|\"").Items[0].InsertText);
        }

        [Fact]
        [DisplayName("Inside any other string literal nothing is offered")]
        public void At_OtherStringLiteral_OffersNothing()
        {
            Assert.Empty(At("note == \"ab|").Items);
            Assert.Empty(At("note.Contains(\"x|").Items);
        }

        [Theory]
        [InlineData("a", 1, true)]
        [InlineData("amount > n", 10, true)]
        [InlineData("am", 2, false)]
        [InlineData("x1", 2, false)]
        [InlineData("1", 1, false)]
        [InlineData("note == \"a", 10, false)]
        [DisplayName("The list opens on its own only at the first letter of a name, outside a string literal")]
        public void StartsName_Positions(string text, int offset, bool expected)
        {
            Assert.Equal(expected, ExpressionCompletion.StartsName(text, offset));
        }
    }
}
