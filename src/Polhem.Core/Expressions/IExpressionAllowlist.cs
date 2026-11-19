namespace Polhem.Core.Expressions
{
    /// <summary>
    /// Checks that an expression uses only what an expression from an untrusted author may use: its variables, the
    /// engine's helper functions, operators, literals and a fixed set of type members.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An <see cref="IExpressionEvaluator"/> resolves members by reflection, so parsing alone does not keep an
    /// expression from reaching further than form logic needs, nor from allocating without bound. An expression from
    /// a definition file is trusted because remote callers cannot write those files; one written by a language model,
    /// or by a user of the application, is not, and must pass this check before it is accepted.
    /// </para>
    /// <para>
    /// An evaluator that can perform the check implements this interface alongside <see cref="IExpressionEvaluator"/>.
    /// A caller that must enforce the check treats an evaluator without it as one that rejects everything.
    /// </para>
    /// </remarks>
    public interface IExpressionAllowlist
    {
        /// <summary>
        /// Returns what <paramref name="expression"/> uses outside the allowlist; empty when it uses nothing else.
        /// </summary>
        /// <param name="expression">The expression text.</param>
        /// <param name="variableTypes">The variables the expression may name, with their types.</param>
        /// <param name="returnType">The type the expression must produce.</param>
        /// <returns>The violations, in no particular order.</returns>
        /// <remarks>
        /// The size limits (length and nesting depth) are checked on the text before it is parsed, so an expression
        /// built to exhaust the parser is reported, not parsed. An expression that does not parse otherwise yields
        /// no violation here: <see cref="IExpressionEvaluator.Validate"/> reports it.
        /// </remarks>
        IReadOnlyList<ExpressionViolation> FindViolations(string expression,
            IReadOnlyDictionary<string, Type> variableTypes, Type returnType);
    }
}
