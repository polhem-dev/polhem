using System.ComponentModel;
using Polhem.Base;
using Polhem.Base.Expressions;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// Tests for <see cref="DynamicExpressoEvaluator"/>: arithmetic, boolean conditions, type conversion, helper functions,
    /// referenced variable detection, compilation cache consistency and sandbox blocking.
    /// </summary>
    public class DynamicExpressoEvaluatorTests
    {
        private readonly DynamicExpressoEvaluator _evaluator = new();

        private static Dictionary<string, object?> Vars(params (string Name, object? Value)[] pairs)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (name, value) in pairs) { dict[name] = value; }
            return dict;
        }

        [Fact]
        [DisplayName("Now() returns the current UTC time under the Utc basis and the current time in the user zone under the UserZone basis")]
        public void Evaluate_NowFunction_FollowsBasis()
        {
            const string kiritimati = "Pacific/Kiritimati";   // UTC+14, so the two bases always differ by 14 hours.
            var variables = Vars();
            var utcBefore = DateTime.UtcNow;
            var zoneBefore = FrameworkClock.Now(kiritimati);

            var utc = _evaluator.Evaluate<DateTime>("Now()", variables, kiritimati, DateTimeBasis.Utc);
            var userZone = _evaluator.Evaluate<DateTime>("Now()", variables, kiritimati);

            Assert.InRange(utc, utcBefore, DateTime.UtcNow);
            Assert.InRange(userZone, zoneBefore, FrameworkClock.Now(kiritimati));
        }

        [Theory]
        [InlineData("Pacific/Kiritimati")]
        [InlineData("Pacific/Pago_Pago")]
        [DisplayName("Today() still returns today in the user time zone under the Utc basis; the basis only affects Now()")]
        public void Evaluate_TodayFunction_IgnoresBasis(string timeZoneId)
        {
            // At any moment at least one of the two zones has a today that differs from UTC,
            // so the assertion is never vacuous.
            var result = _evaluator.Evaluate<DateOnly>("Today()", Vars(), timeZoneId, DateTimeBasis.Utc);

            Assert.Equal(FrameworkClock.Today(timeZoneId), result);
        }

        [Fact]
        [DisplayName("Field arithmetic unit_price * qty returns the product as decimal")]
        public void Evaluate_Arithmetic_ReturnsProduct()
        {
            var result = _evaluator.Evaluate<decimal>(
                "unit_price * qty", Vars(("unit_price", 10m), ("qty", 3m)));

            Assert.Equal(30m, result);
        }

        [Theory]
        [InlineData(5, true)]
        [InlineData(-1, false)]
        [DisplayName("Boolean condition amount > 0 returns true or false by value")]
        public void Evaluate_BoolCondition_ReturnsExpected(int amount, bool expected)
        {
            var result = _evaluator.Evaluate<bool>("amount > 0", Vars(("amount", (decimal)amount)));

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("An int expression result is converted to the decimal return type")]
        public void Evaluate_IntExpressionToDecimalReturn_Converts()
        {
            var result = _evaluator.Evaluate<decimal>("qty * 2", Vars(("qty", 3)));

            Assert.IsType<decimal>(result);
            Assert.Equal(6m, result);
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("x", false)]
        [DisplayName("String helper IsNullOrEmpty(name) returns the result for the value")]
        public void Evaluate_StringHelperFunction_ReturnsExpected(string name, bool expected)
        {
            var result = _evaluator.Evaluate<bool>("IsNullOrEmpty(name)", Vars(("name", name)));

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("Today() returns DateOnly, the type used for dates outside a DataSet")]
        public void Evaluate_TodayFunction_ReturnsDateOnly()
        {
            var result = _evaluator.Evaluate<DateOnly>(
                "Today()", new Dictionary<string, object?>(StringComparer.Ordinal));

            // Without a time zone, the evaluator uses UTC as its basis (see `FrameworkClock`).
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), result);
        }

        [Fact]
        [DisplayName("Today() evaluates in the time zone passed by the caller, not the machine time zone")]
        public void Evaluate_TodayFunction_UsesSuppliedTimeZone()
        {
            // The zone is far enough from UTC that its local today differs from the UTC today for most of the day.
            // The assertion still holds when they coincide, because it compares with that zone's today, not with UTC.
            // One evaluator serves every time zone: the zone is an argument, not evaluator state (ADR-032 D13).
            var expected = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati")));

            var result = _evaluator.Evaluate<DateOnly>(
                "Today()", new Dictionary<string, object?>(StringComparer.Ordinal), "Pacific/Kiritimati");

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("UtcNow() returns the current UTC time with DateTimeKind.Unspecified")]
        public void Evaluate_UtcNowFunction_ReturnsUnspecifiedKind()
        {
            var result = _evaluator.Evaluate<DateTime>(
                "UtcNow()", new Dictionary<string, object?>(StringComparer.Ordinal));

            // The kind is `Unspecified` because `Local` shifts the reading on both wires (ADR-032 D6).
            Assert.Equal(DateTimeKind.Unspecified, result.Kind);
            Assert.True(Math.Abs((DateTime.UtcNow - result).TotalMinutes) < 1);
        }

        [Fact]
        [DisplayName("GetReferencedVariables returns field variables and excludes registered functions")]
        public void GetReferencedVariables_ReturnsFieldNamesOnly()
        {
            var referenced = _evaluator.GetReferencedVariables("qty > 0 && Today() > order_date");

            Assert.Contains("qty", referenced);
            Assert.Contains("order_date", referenced);
            // `Today` is a registered function, so it is not reported as a variable.
            Assert.DoesNotContain("Today", referenced);
        }

        [Fact]
        [DisplayName("Evaluating the same cached expression repeatedly gives correct results for different inputs")]
        public void Evaluate_SameExpressionReused_ProducesCorrectResultsAcrossInputs()
        {
            var first = _evaluator.Evaluate<decimal>(
                "unit_price * qty", Vars(("unit_price", 10m), ("qty", 2m)));
            var second = _evaluator.Evaluate<decimal>(
                "unit_price * qty", Vars(("unit_price", 7m), ("qty", 3m)));

            Assert.Equal(20m, first);
            Assert.Equal(21m, second);
        }

        [Theory]
        [InlineData("11111111-1111-1111-1111-111111111111", true)]
        [InlineData("00000000-0000-0000-0000-000000000000", false)]
        [DisplayName("Guid comparison customer_rowid != Guid.Empty returns the result for the value")]
        public void Evaluate_GuidComparison_ReturnsExpected(string guid, bool expected)
        {
            var result = _evaluator.Evaluate<bool>(
                "customer_rowid != Guid.Empty", Vars(("customer_rowid", Guid.Parse(guid))));

            Assert.Equal(expected, result);
        }

        [Fact]
        [DisplayName("The sandbox throws ExpressionEvaluationException for access to an unregistered type (System.IO.File)")]
        public void Evaluate_UnregisteredType_ThrowsExpressionEvaluationException()
        {
            var ex = Assert.Throws<ExpressionEvaluationException>(() =>
                _evaluator.Evaluate<string>(
                    "System.IO.File.ReadAllText(\"secret.txt\")",
                    new Dictionary<string, object?>(StringComparer.Ordinal)));

            Assert.Equal("System.IO.File.ReadAllText(\"secret.txt\")", ex.Expression);
        }

        [Fact]
        [DisplayName("A malformed expression throws ExpressionEvaluationException")]
        public void Evaluate_MalformedExpression_ThrowsExpressionEvaluationException()
        {
            Assert.Throws<ExpressionEvaluationException>(() =>
                _evaluator.Evaluate<decimal>("unit_price *", Vars(("unit_price", 1m))));
        }
    }
}
