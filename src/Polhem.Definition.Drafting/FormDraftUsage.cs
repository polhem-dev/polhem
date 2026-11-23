namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// What one turn of a <see cref="FormDraftSession"/> has cost so far: the calls to the model, the tokens it reported
    /// and the time since the turn began.
    /// </summary>
    /// <param name="Calls">The calls to the model, including the one in progress.</param>
    /// <param name="InputTokens">
    /// The input tokens the model reported for the calls that returned, those read from the provider's cache included;
    /// 0 when it reports none.
    /// </param>
    /// <param name="OutputTokens">The output tokens the model reported for the calls that returned; 0 when it reports none.</param>
    /// <param name="CachedInputTokens">
    /// The part of <paramref name="InputTokens"/> the provider read from its cache, which it usually charges less for;
    /// 0 when it reports none.
    /// </param>
    /// <param name="Elapsed">The time since the turn began.</param>
    public sealed record FormDraftUsage(int Calls, long InputTokens, long OutputTokens, long CachedInputTokens, TimeSpan Elapsed)
    {
        /// <summary>A turn that has not called the model.</summary>
        public static FormDraftUsage None { get; } = new(0, 0, 0, 0, TimeSpan.Zero);
    }
}
