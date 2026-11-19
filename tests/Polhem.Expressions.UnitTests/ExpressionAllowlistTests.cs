using System.ComponentModel;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// Tests for the allowlist <see cref="DynamicExpressoEvaluator.FindViolations"/> enforces: what form logic needs
    /// passes, and reflection, unbounded allocation, sequences and anything outside the list are reported.
    /// </summary>
    public class ExpressionAllowlistTests
    {
        private readonly DynamicExpressoEvaluator _evaluator = new();

        private static readonly Dictionary<string, Type> s_variables = new(StringComparer.Ordinal)
        {
            ["name"] = typeof(string),
            ["status"] = typeof(string),
            ["amount"] = typeof(decimal),
            ["unit_price"] = typeof(decimal),
            ["discount"] = typeof(decimal),
            ["quantity"] = typeof(int),
            ["d"] = typeof(DateTime),
            ["d2"] = typeof(DateTime),
            ["flag"] = typeof(bool),
            ["customer_rowid"] = typeof(Guid),
            ["photo"] = typeof(byte[]),
            ["__agg0"] = typeof(int),
        };

        private IReadOnlyList<Core.Expressions.ExpressionViolation> Check(string expression) =>
            _evaluator.FindViolations(expression, s_variables, typeof(object));

        [Theory]
        [DisplayName("An expression form logic needs uses nothing outside the allowlist")]
        [InlineData("quantity * unit_price * (1 - discount)")]
        [InlineData("customer_rowid != Guid.Empty")]
        [InlineData("Today().AddDays(7)")]
        [InlineData("d < Now()")]
        [InlineData("IsBlank(name) || IsNullOrWhiteSpace(status)")]
        [InlineData("Between(amount, 1, 100)")]
        [InlineData("In(status, \"Draft\", \"Rejected\")")]
        [InlineData("DaysBetween(d, Today()) <= 30")]
        [InlineData("MonthEnd(d) >= d2")]
        [InlineData("RegexMatch(name, \"^[0-9]+$\")")]
        [InlineData("name.Length > 3 && name.Trim().ToUpper() == \"X\"")]
        [InlineData("name.Contains(\"a\") || name.StartsWith(\"b\") || name.Substring(1) == \"c\"")]
        [InlineData("RegexMatch(name, \"^[0-9]{2,4}-[0-9]{6,8}$\") || RegexMatch(name, \"^(ab|cd)+x?$\")")]
        [InlineData("RegexMatch(name, \"^\\\\d{3}-\\\\d{4}$\") || RegexMatch(name, \"^(?i)[a-z]+@[a-z]+\\\\.com$\")")]
        [InlineData("name + name + name + name + name + name + name + name")]
        [InlineData("Math.Round(amount, 2) + Math.Abs(amount) + Math.Max(amount, 1m)")]
        [InlineData("amount * 1.05m")]
        [InlineData("d.Year == 2026 && d.AddMonths(1).Month > 1 && d.Date <= d2")]
        [InlineData("(d2 - d).TotalDays > 1")]
        [InlineData("new DateTime(2026, 1, 1) <= d")]
        [InlineData("status == \"A\" ? 1 : 0")]
        [InlineData("Convert.ToDecimal(quantity) * 2m")]
        [InlineData("name + \"x\" + quantity")]
        [InlineData("quantity.ToString() == amount.ToString()")]
        [InlineData("name.ToString() + Today().ToString() + d.ToString(\"yyyy\")")]
        [InlineData("!flag && quantity > 0")]
        [InlineData("__agg0 > 0")]
        [InlineData("photo == null")]
        public void FindViolations_FormLogic_None(string expression)
        {
            Assert.Empty(Check(expression));
        }

        [Theory]
        [DisplayName("Reflection, unbounded allocation, sequences and members outside the list are reported where they are written")]
        [InlineData("name.GetType()", "GetType")]
        [InlineData("typeof(string) == null", "typeof")]
        [InlineData("DateTime.Now", "Now")]
        [InlineData("Convert.ChangeType(name, typeof(int))", "ChangeType")]
        [InlineData("Enumerable.Range(0, 10)", "Range")]
        [InlineData("new string('a', 10)", "new")]
        [InlineData("name.PadLeft(10)", "PadLeft")]
        [InlineData("string.Format(\"{0}\", name)", "Format")]
        [InlineData("name.Split(',')", "Split")]
        [InlineData("Guid.NewGuid()", "NewGuid")]
        [InlineData("name.GetHashCode()", "GetHashCode")]
        [InlineData("string.Join(\",\", name, status)", "Join")]
        [InlineData("decimal.Parse(name)", "Parse")]
        public void FindViolations_OutsideTheList_ReportedWhereWritten(string expression, string word)
        {
            var violations = Check(expression);

            Assert.Contains(violations, violation => violation.Position == expression.IndexOf(word, StringComparison.Ordinal));
        }

        [Theory]
        [DisplayName("A RegexMatch pattern that nests repetition, counts too high or is not a literal is reported")]
        [InlineData("RegexMatch(name, \"((a|b){0,20}){0,20}c\")", "itself repeats")]
        [InlineData("RegexMatch(name, \"^(a+)+$\")", "itself repeats")]
        [InlineData("RegexMatch(name, \"((a)*b)*\")", "itself repeats")]
        [InlineData("RegexMatch(name, \"a{0,21}\")", "more than 20")]
        [InlineData("RegexMatch(name, \"([ab]{0,20}){0,20}c\")", "itself repeats")]
        [InlineData("RegexMatch(name, \"(\\\\w{0,20}){0,20}c\")", "itself repeats")]
        [InlineData("RegexMatch(name, \"(?i)([a-z]{0,20}){0,20}c\")", "itself repeats")]
        [InlineData("RegexMatch(name, \"[ab]{9000}c\")", "more than 20")]
        [InlineData("RegexMatch(name, \"\\\\d{21}\")", "more than 20")]
        [InlineData("RegexMatch(name, \"(?x)(a{0,20}|b{0,20}) {0,20}c\")", "x option")]
        [InlineData("RegexMatch(name, \"(?#c)a\")", "comments")]
        [InlineData("RegexMatch(name, \"a{1000000}\")", "more than 20")]
        [InlineData("RegexMatch(name, status)", "text literal")]
        public void FindViolations_RegexPattern_Reported(string expression, string expected)
        {
            Assert.Contains(Check(expression), violation => violation.Message.Contains(expected, StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Joining text more than the limit is reported, since the cost grows with the square of the joins")]
        public void FindViolations_TooManyConcatenations_Reported()
        {
            var expression = string.Join(" + ", Enumerable.Repeat("name", 10));

            Assert.Contains(Check(expression), violation => violation.Message.Contains("joins text", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("IndexOf is reported: it compares by culture, in time that grows with both texts")]
        public void FindViolations_IndexOf_Reported()
        {
            Assert.NotEmpty(Check("name.IndexOf(status) >= 0"));
        }

        [Theory]
        [DisplayName("A number formatted with a format string is reported, since its precision has no upper bound")]
        [InlineData("amount.ToString(\"F99999999\").Length > 0")]
        [InlineData("quantity.ToString(\"D9\") == \"x\"")]
        public void FindViolations_NumberFormat_Reported(string expression)
        {
            Assert.NotEmpty(Check(expression));
        }

        [Theory]
        [DisplayName("A type test or cast to a type outside the value types is reported")]
        [InlineData("name is string")]
        [InlineData("name as object == null")]
        public void FindViolations_TypeTests_Reported(string expression)
        {
            Assert.NotEmpty(Check(expression));
        }

        [Fact]
        [DisplayName("An expression longer than the limit is reported without being parsed")]
        public void FindViolations_TooLong_Reported()
        {
            var expression = string.Join(" + ", Enumerable.Repeat("amount", 200));

            var violation = Assert.Single(Check(expression));

            Assert.Contains("longer than", violation.Message, StringComparison.Ordinal);
            Assert.Null(violation.Position);
        }

        [Fact]
        [DisplayName("Nesting deeper than the limit is reported where it goes too deep, before the parser could overflow")]
        public void FindViolations_TooDeep_Reported()
        {
            var expression = new string('(', 17) + "amount" + new string(')', 17);

            var violation = Assert.Single(Check(expression));

            Assert.Contains("nests deeper", violation.Message, StringComparison.Ordinal);
            Assert.Equal(16, violation.Position);
        }

        [Fact]
        [DisplayName("Parentheses inside a text literal do not count towards the nesting limit")]
        public void FindViolations_ParenthesesInLiteral_NotCounted()
        {
            Assert.Empty(Check("name == \"" + new string('(', 40) + "\""));
        }

        [Fact]
        [DisplayName("A text literal longer than the limit is reported")]
        public void FindViolations_LongLiteral_Reported()
        {
            var violation = Assert.Single(Check("name == \"" + new string('x', 201) + "\""));

            Assert.Contains("text literal", violation.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An expression that does not parse yields no violation, leaving the parse error to Validate")]
        public void FindViolations_DoesNotParse_None()
        {
            Assert.Empty(Check("amount +"));
        }

        [Fact]
        [DisplayName("A helper function's name used as a value yields no finding and fails Validate with the contract's exception")]
        public void HelperNameAsValue_NoFindingAndValidateThrowsExpressionError()
        {
            Assert.Empty(Check("IsBlank"));
            Assert.Throws<Core.Expressions.ExpressionEvaluationException>(() =>
                _evaluator.Validate("IsBlank", s_variables, typeof(object)));
        }

        [Fact]
        [DisplayName("A field named like a type is reported, since the run would read the name as the type")]
        public void FindViolations_FieldNamedLikeType_Reported()
        {
            var variables = new Dictionary<string, Type>(StringComparer.Ordinal) { ["Math"] = typeof(string) };

            var violation = Assert.Single(_evaluator.FindViolations("Math.Length > 0", variables, typeof(bool)));

            Assert.Contains("'Math'", violation.Message, StringComparison.Ordinal);
            Assert.Equal(0, violation.Position);
        }

        [Fact]
        [DisplayName("IsAllowedMember accepts a listed member and rejects one outside the list")]
        public void IsAllowedMember_ListedAndUnlisted()
        {
            Assert.True(DynamicExpressoEvaluator.IsAllowedMember(typeof(string).GetProperty(nameof(string.Length))!));
            Assert.False(DynamicExpressoEvaluator.IsAllowedMember(typeof(object).GetMethod(nameof(GetType))!));
        }
    }
}
