namespace Polhem.AI.Claude
{
    /// <summary>
    /// How much effort the model spends on each answer: how deeply it thinks and how many tokens it uses.
    /// </summary>
    public enum ClaudeEffort
    {
        /// <summary>The fewest tokens; for simple tasks.</summary>
        Low,

        /// <summary>A balance of cost and depth; the default of current Opus models when none is sent.</summary>
        Medium,

        /// <summary>Thorough work on tasks that need judgement.</summary>
        High,

        /// <summary>More thorough than <see cref="High"/>, for long agentic work.</summary>
        ExtraHigh,

        /// <summary>The most thorough, when correctness matters more than cost.</summary>
        Max,
    }
}
