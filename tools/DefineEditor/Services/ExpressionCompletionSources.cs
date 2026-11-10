using Polhem.Expressions;

namespace Polhem.DefineEditor.Services;

/// <summary>What an expression can name, read from the framework by the expression editor.</summary>
/// <param name="Variables">The fields of the table by declared name, typed as the evaluator types them.</param>
/// <param name="Functions">The helper functions (<see cref="DynamicExpressoEvaluator.HelperFunctions"/>).</param>
/// <param name="Types">The type names the evaluator accepts (<see cref="DynamicExpressoEvaluator.ReferencedTypes"/>).</param>
/// <param name="ListValues">A drop-down field's values by field name; <c>null</c> for a field without a list.</param>
public sealed record ExpressionCompletionSources(
    IReadOnlyDictionary<string, Type> Variables,
    IReadOnlyList<ExpressionFunction> Functions,
    IReadOnlyDictionary<string, Type> Types,
    Func<string, IReadOnlyList<string>?> ListValues);
