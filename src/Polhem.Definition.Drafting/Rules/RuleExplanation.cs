namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// A plain-language explanation of an existing rule, with sample rows the expression engine computed.
    /// </summary>
    /// <param name="Explanation">What the rule does, in the language the user reads.</param>
    /// <param name="Samples">Rows of sample values with what the rule does with each.</param>
    public sealed record RuleExplanation(string Explanation, IReadOnlyList<RuleDraftSample> Samples);
}
