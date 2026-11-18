using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Polhem.Expressions.UnitTests
{
    /// <summary>
    /// The description of every helper function in <see cref="DynamicExpressoEvaluator.HelperFunctions"/>: a definition
    /// editor shows it and a tool that writes expressions chooses functions by it, so each one must be present and its
    /// example must still parse.
    /// </summary>
    public class HelperFunctionCatalogTests
    {
        private readonly DynamicExpressoEvaluator _evaluator = new(preferInterpretation: true);

        public static TheoryData<string> FunctionNames()
        {
            var data = new TheoryData<string>();
            foreach (var function in DynamicExpressoEvaluator.HelperFunctions) data.Add(function.Name);
            return data;
        }

        private static ExpressionFunction Function(string name) =>
            DynamicExpressoEvaluator.HelperFunctions.Single(function => function.Name == name);

        [Theory]
        [MemberData(nameof(FunctionNames))]
        [DisplayName("Every helper function has a one-sentence summary")]
        public void HelperFunctions_EachFunction_HasSummarySentence(string name)
        {
            var summary = Function(name).Summary;

            Assert.False(string.IsNullOrWhiteSpace(summary));
            Assert.EndsWith(".", summary, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(FunctionNames))]
        [DisplayName("Every helper function's example calls the function")]
        public void HelperFunctions_EachExample_CallsTheFunction(string name)
        {
            var example = Function(name).Example;

            Assert.Matches(new Regex($@"(?<![\w.]){Regex.Escape(name)}\s*\(", RegexOptions.None, TimeSpan.FromSeconds(1)),
                example);
        }

        [Theory]
        [MemberData(nameof(FunctionNames))]
        [DisplayName("Every helper function's example parses with the fields it names")]
        public void HelperFunctions_EachExample_Parses(string name)
        {
            var exception = Record.Exception(() => _evaluator.Validate(Function(name).Example,
                DynamicExpressoEvaluator.GetExampleFields(name), typeof(object)));

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("An example naming a field it does not declare fails to parse, so the parse test can catch it")]
        public void Validate_ExampleWithUndeclaredField_Throws()
        {
            var exception = Record.Exception(() =>
                _evaluator.Validate("IsNullOrEmpty(remark)", new Dictionary<string, Type>(), typeof(object)));

            Assert.NotNull(exception);
        }
    }
}
