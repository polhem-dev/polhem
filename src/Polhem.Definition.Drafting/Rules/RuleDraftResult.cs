namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// The outcome of one request to draft or revise a rule.
    /// </summary>
    /// <param name="Draft">The rule, when the model wrote one that passed every check; <see langword="null"/> otherwise.</param>
    /// <param name="Reply">What the model said to the user, such as a question about what the rule should do, or why it
    /// could not write it; empty when the rule was accepted, since the request ends there.</param>
    /// <param name="Usage">What the request cost: the calls to the model, the tokens and the time.</param>
    public sealed record RuleDraftResult(RuleDraft? Draft, string Reply, RuleDraftingUsage Usage);
}
