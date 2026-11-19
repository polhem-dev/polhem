namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// The outcome of a request to explain a rule.
    /// </summary>
    /// <param name="Explanation">The explanation, when the model wrote one whose samples the engine confirmed;
    /// <see langword="null"/> otherwise.</param>
    /// <param name="Reply">What the model said besides, such as why it could not explain the rule; may be empty.</param>
    public sealed record RuleExplanationResult(RuleExplanation? Explanation, string Reply);
}
