namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// What one request of <see cref="RuleDrafting"/> has cost so far: the calls to the model, the tokens it reported and
    /// the time since the request began.
    /// </summary>
    /// <param name="Calls">The calls to the model, including the one in progress.</param>
    /// <param name="InputTokens">The input tokens the model reported for the calls that returned; 0 when it reports none.</param>
    /// <param name="OutputTokens">The output tokens the model reported for the calls that returned; 0 when it reports none.</param>
    /// <param name="Elapsed">The time since the request began.</param>
    public sealed record RuleDraftingUsage(int Calls, long InputTokens, long OutputTokens, TimeSpan Elapsed)
    {
        /// <summary>A request that has not called the model.</summary>
        public static RuleDraftingUsage None { get; } = new(0, 0, 0, TimeSpan.Zero);
    }
}
