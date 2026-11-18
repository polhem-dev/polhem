using System.ComponentModel;
using Polhem.Core.Expressions;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// The business helper functions as an expression calls them: <c>IsBlank</c>, <c>Between</c>, <c>In</c>,
    /// <c>DaysBetween</c>, <c>MonthEnd</c> and <c>RegexMatch</c>, against variables typed as a form row types them.
    /// </summary>
    public class BusinessFunctionTests
    {
        private static readonly DateTime s_orderDate = new(2026, 2, 10, 15, 30, 0, DateTimeKind.Unspecified);

        public static TheoryData<bool> Modes() => [false, true];

        private static T Evaluate<T>(bool interpret, string expression, params (string Name, object? Value)[] variables)
        {
            var evaluator = new DynamicExpressoEvaluator(preferInterpretation: interpret);
            return evaluator.Evaluate<T>(expression,
                variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal))!;
        }

        [Theory]
        [DisplayName("IsBlank treats an empty or blank text, an empty key, zero and the minimum date as blank")]
        [InlineData("")]
        [InlineData("   ")]
        public void IsBlank_BlankText_True(string text)
        {
            Assert.True(Evaluate<bool>(false, "IsBlank(v)", ("v", text)));
        }

        [Fact]
        [DisplayName("IsBlank is true for zero, an empty key and the minimum date, and false for filled values and booleans")]
        public void IsBlank_ByType_FollowsFilledInRule()
        {
            Assert.True(Evaluate<bool>(false, "IsBlank(v)", ("v", 0m)));
            Assert.True(Evaluate<bool>(false, "IsBlank(v)", ("v", 0)));
            Assert.True(Evaluate<bool>(false, "IsBlank(v)", ("v", Guid.Empty)));
            Assert.True(Evaluate<bool>(false, "IsBlank(v)", ("v", DateTime.MinValue)));
            Assert.False(Evaluate<bool>(false, "IsBlank(v)", ("v", "A001")));
            Assert.False(Evaluate<bool>(false, "IsBlank(v)", ("v", 12.5m)));
            Assert.False(Evaluate<bool>(false, "IsBlank(v)", ("v", Guid.NewGuid())));
            Assert.False(Evaluate<bool>(false, "IsBlank(v)", ("v", s_orderDate)));
            Assert.False(Evaluate<bool>(false, "IsBlank(v)", ("v", false)));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        [DisplayName("Between compares a decimal field with whole-number literals, both ends included")]
        public void Between_DecimalWithIntegerLiterals_InclusiveRange(bool interpret)
        {
            Assert.True(Evaluate<bool>(interpret, "Between(amount, 1, 100)", ("amount", 100m)));
            Assert.True(Evaluate<bool>(interpret, "Between(amount, 1, 100)", ("amount", 1m)));
            Assert.False(Evaluate<bool>(interpret, "Between(amount, 1, 100)", ("amount", 100.01m)));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        [DisplayName("Between compares a date field with Today(), a DateOnly, as calendar instants")]
        public void Between_DateFieldWithToday_Compares(bool interpret)
        {
            var today = DateTime.UtcNow.Date;

            Assert.True(Evaluate<bool>(interpret, "Between(due, Today(), Today().AddDays(7))", ("due", today.AddDays(3))));
            Assert.False(Evaluate<bool>(interpret, "Between(due, Today(), Today().AddDays(7))", ("due", today.AddDays(-1))));
        }

        [Fact]
        [DisplayName("Between compares texts ordinally")]
        public void Between_Texts_Ordinal()
        {
            Assert.True(Evaluate<bool>(false, "Between(code, \"A\", \"C\")", ("code", "B9")));
            Assert.False(Evaluate<bool>(false, "Between(code, \"A\", \"C\")", ("code", "c")));
        }

        [Fact]
        [DisplayName("Between rejects values of different kinds with an expression error naming both")]
        public void Between_MixedKinds_ThrowsExpressionError()
        {
            var ex = Assert.Throws<ExpressionEvaluationException>(() =>
                Evaluate<bool>(false, "Between(code, 1, 10)", ("code", "B9")));

            Assert.Contains("String", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Int32", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        [DisplayName("In matches a text among the listed values and a decimal among whole-number literals")]
        public void In_ListedValues_Matches(bool interpret)
        {
            Assert.True(Evaluate<bool>(interpret, "In(status, \"Draft\", \"Rejected\")", ("status", "Rejected")));
            Assert.False(Evaluate<bool>(interpret, "In(status, \"Draft\", \"Rejected\")", ("status", "Approved")));
            Assert.True(Evaluate<bool>(interpret, "In(rate, 0, 5, 10)", ("rate", 5.00m)));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        [DisplayName("DaysBetween counts calendar days between any mix of date fields and Today()")]
        public void DaysBetween_DateTimeAndDateOnly_CountsDays(bool interpret)
        {
            var later = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal(19, Evaluate<int>(interpret, "DaysBetween(a, b)", ("a", s_orderDate), ("b", later)));
            Assert.Equal(-19, Evaluate<int>(interpret, "DaysBetween(b, a)", ("a", s_orderDate), ("b", later)));
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - DateOnly.FromDateTime(s_orderDate).DayNumber,
                Evaluate<int>(interpret, "DaysBetween(a, Today())", ("a", s_orderDate)));
            Assert.Equal(DateOnly.FromDateTime(s_orderDate).DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber,
                Evaluate<int>(interpret, "DaysBetween(Today(), a)", ("a", s_orderDate)));
            Assert.Equal(1, Evaluate<int>(interpret, "DaysBetween(Today(), Today().AddDays(1))"));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        [DisplayName("MonthEnd returns the last day of the month for a date field and for Today()")]
        public void MonthEnd_DateTimeAndDateOnly_LastDay(bool interpret)
        {
            Assert.Equal(new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Unspecified),
                Evaluate<DateTime>(interpret, "MonthEnd(d)", ("d", s_orderDate)));
            Assert.Equal(29, Evaluate<DateTime>(interpret, "MonthEnd(d)",
                ("d", new DateTime(2028, 2, 3, 0, 0, 0, DateTimeKind.Unspecified))).Day);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            Assert.Equal(DateTime.DaysInMonth(today.Year, today.Month), Evaluate<DateOnly>(interpret, "MonthEnd(Today())").Day);
        }

        [Fact]
        [DisplayName("RegexMatch matches a pattern and never matches an empty text")]
        public void RegexMatch_Pattern_Matches()
        {
            Assert.True(Evaluate<bool>(false, "RegexMatch(phone, \"^[0-9-]+$\")", ("phone", "02-1234-5678")));
            Assert.False(Evaluate<bool>(false, "RegexMatch(phone, \"^[0-9-]+$\")", ("phone", "02 1234")));
            Assert.False(Evaluate<bool>(false, "RegexMatch(phone, \".*\")", ("phone", "")));
        }

        [Fact]
        [DisplayName("RegexMatch with an invalid pattern raises an expression error")]
        public void RegexMatch_InvalidPattern_ThrowsExpressionError()
        {
            Assert.Throws<ExpressionEvaluationException>(() =>
                Evaluate<bool>(false, "RegexMatch(phone, \"[\")", ("phone", "1")));
        }
    }
}
