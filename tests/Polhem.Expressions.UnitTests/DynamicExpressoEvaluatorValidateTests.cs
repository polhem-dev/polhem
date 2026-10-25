using System.ComponentModel;
using Polhem.Core.Expressions;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// Tests for <see cref="DynamicExpressoEvaluator.Validate"/>: an expression is parsed against variable types with
    /// the same interpreter configuration as evaluation, and nothing is compiled.
    /// </summary>
    public class DynamicExpressoEvaluatorValidateTests
    {
        private readonly DynamicExpressoEvaluator _evaluator = new(preferInterpretation: true);

        private static Dictionary<string, Type> Types(params (string Name, Type Type)[] pairs)
            => pairs.ToDictionary(p => p.Name, p => p.Type, StringComparer.Ordinal);

        [Fact]
        [DisplayName("Validate rejects a decimal variable multiplied by a double literal, naming both operand types")]
        public void Validate_DecimalTimesDoubleLiteral_Throws()
        {
            var ex = Assert.Throws<ExpressionEvaluationException>(() =>
                _evaluator.Validate("amount * 1.05", Types(("amount", typeof(decimal))), typeof(object)));

            Assert.Equal("amount * 1.05", ex.Expression);
            Assert.Contains("Decimal", ex.Message);
            Assert.Contains("Double", ex.Message);
        }

        [Fact]
        [DisplayName("Validate accepts a decimal variable multiplied by a decimal literal")]
        public void Validate_DecimalTimesDecimalLiteral_Passes()
        {
            var exception = Record.Exception(() =>
                _evaluator.Validate("amount * 1.05m", Types(("amount", typeof(decimal))), typeof(object)));

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("Validate rejects an identifier that is not among the variables")]
        public void Validate_UnknownIdentifier_Throws()
        {
            Assert.Throws<ExpressionEvaluationException>(() =>
                _evaluator.Validate("amount * qty", Types(("amount", typeof(decimal))), typeof(object)));
        }

        [Fact]
        [DisplayName("Validate accepts the registered helper functions and Guid, as evaluation does")]
        public void Validate_HelperFunctionsAndGuid_Pass()
        {
            var types = Types(("customer_rowid", typeof(Guid)), ("note", typeof(string)));

            Assert.Null(Record.Exception(() => _evaluator.Validate("Today().AddDays(7)", types, typeof(object))));
            Assert.Null(Record.Exception(() =>
                _evaluator.Validate("customer_rowid != Guid.Empty && !IsNullOrEmpty(note)", types, typeof(bool))));
        }

        [Fact]
        [DisplayName("Validate rejects an expression whose result cannot convert to the requested return type")]
        public void Validate_ResultNotConvertibleToReturnType_Throws()
        {
            Assert.Throws<ExpressionEvaluationException>(() =>
                _evaluator.Validate("note", Types(("note", typeof(string))), typeof(bool)));
        }

        [Fact]
        [DisplayName("Validate compiles nothing and leaves the compiled-expression cache empty")]
        public void Validate_PassingExpression_DoesNotPopulateCompiledCache()
        {
            _evaluator.Validate("amount * 2", Types(("amount", typeof(decimal))), typeof(object));

            Assert.Empty(_evaluator.GetCompiledInvokers());
        }
    }
}
