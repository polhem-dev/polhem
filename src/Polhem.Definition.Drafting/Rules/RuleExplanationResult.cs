namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// The outcome of a request to explain a rule.
    /// </summary>
    /// <param name="Explanation">The explanation, when the model wrote one whose samples the engine confirmed;
    /// <see langword="null"/> otherwise.</param>
    /// <param name="Reply">What the model said besides, such as why it could not explain the rule; empty when the
    /// explanation was accepted.</param>
    /// <param name="Usage">What the request cost: the calls to the model, the tokens and the time.</param>
    public sealed record RuleExplanationResult(RuleExplanation? Explanation, string Reply, RuleDraftingUsage Usage);
}
