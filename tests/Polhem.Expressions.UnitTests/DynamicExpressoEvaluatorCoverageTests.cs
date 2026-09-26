using System.ComponentModel;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// Coverage tests for <see cref="DynamicExpressoEvaluator"/>: the null variable value branch, a null return value,
    /// and the zero and multiple identifier edges of GetReferencedVariables.
    /// </summary>
    public class DynamicExpressoEvaluatorCoverageTests
    {
        private readonly DynamicExpressoEvaluator _evaluator = new();

        private static Dictionary<string, object?> Vars(params (string Name, object? Value)[] pairs)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (name, value) in pairs) { dict[name] = value; }
            return dict;
        }

        [Fact]
        [DisplayName("A null variable value falls back to the object type and an empty string argument, and the expression still evaluates")]
        public void Evaluate_NullVariableValue_UsesObjectTypeAndEmptyStringArgument()
        {
            // A null variable value drives the `?? typeof(object)` type fallback (GetOrCompile /
            // BuildCacheKey) and the `?? (object)string.Empty` argument fallback.
            var result = _evaluator.Evaluate<bool>("name == null", Vars(("name", null)));

            // The null argument is replaced with string.Empty, so the comparison is false.
            Assert.False(result);
        }

        [Fact]
        [DisplayName("Evaluate<T> returns default when the expression returns null")]
        public void EvaluateGeneric_NullResult_ReturnsDefault()
        {
            var result = _evaluator.Evaluate<string?>(
                "null", new Dictionary<string, object?>(StringComparer.Ordinal));

            Assert.Null(result);
        }

        [Fact]
        [DisplayName("GetReferencedVariables detects every unknown identifier")]
        public void GetReferencedVariables_MultipleIdentifiers_ReturnsAll()
        {
            var referenced = _evaluator.GetReferencedVariables("a + b + c");

            Assert.Contains("a", referenced);
            Assert.Contains("b", referenced);
            Assert.Contains("c", referenced);
        }

        [Fact]
        [DisplayName("GetReferencedVariables returns an empty set for a constant expression")]
        public void GetReferencedVariables_NoIdentifiers_ReturnsEmpty()
        {
            var referenced = _evaluator.GetReferencedVariables("1 + 2");

            Assert.Empty(referenced);
        }
    }
}
